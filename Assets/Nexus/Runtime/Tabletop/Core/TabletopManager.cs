using System.Collections.Generic;
using UnityEngine;
using Mirror;

namespace Nexus
{
    public class TabletopManager : MonoBehaviour
    {
        private static TabletopManager _activeInstance;
        public static TabletopManager GetActive()
        {
            if (_activeInstance != null && _activeInstance.isActiveAndEnabled)
                return _activeInstance;

            // Only consider active and enabled instances
            var all = Object.FindObjectsOfType<TabletopManager>();
            if (all == null || all.Length == 0) { _activeInstance = null; return null; }

            // 0) Prefer a scene-level manager not under any NetworkPlayer
            for (int i = 0; i < all.Length; i++)
            {
                var t = all[i];
                if (!t.isActiveAndEnabled) continue;
                var owner = t.GetComponentInParent<Nexus.Networking.NetworkPlayer>();
                if (owner == null)
                {
                    _activeInstance = t;
                    return _activeInstance;
                }
            }

            // 1) Prefer instance explicitly bound to the local player
            for (int i = 0; i < all.Length; i++)
            {
                var t = all[i];
                if (!t.isActiveAndEnabled) continue;
                if (t.boundLocalPlayer != null && t.boundLocalPlayer.isLocalPlayer)
                {
                    _activeInstance = t;
                    return _activeInstance;
                }
            }

            // 2) Prefer instance under a local NetworkPlayer hierarchy
            for (int i = 0; i < all.Length; i++)
            {
                var t = all[i];
                if (!t.isActiveAndEnabled) continue;
                var owner = t.GetComponentInParent<Nexus.Networking.NetworkPlayer>();
                if (owner != null && owner.isLocalPlayer)
                {
                    _activeInstance = t;
                    return _activeInstance;
                }
            }

            // 3) Fallback: first active one
            for (int i = 0; i < all.Length; i++)
            {
                var t = all[i];
                if (t.isActiveAndEnabled)
                {
                    _activeInstance = t;
                    return _activeInstance;
                }
            }
            _activeInstance = null;
            return null;
        }

        private void OnEnable() { _activeInstance = null; }
        private void OnDisable() { if (_activeInstance == this) _activeInstance = null; }

        [Header("References")]
        [SerializeField] private Camera mainCamera;

        [Header("Movement Settings")]
        [SerializeField] private float baseDistance = 5f;
        [SerializeField] private float scrollSpeed = 2f;
        [SerializeField] private float rotationSpeed = 100f;
        [SerializeField] private float pickupForce = 10f;
        [SerializeField] private float moveSmoothness = 25f;
        [SerializeField] private float groundSnapOffset = 0.02f;
        [SerializeField] private LayerMask moveSurfaceMask = ~0; // all layers by default
        [SerializeField] private float minForwardDistance = 1.5f;
        [SerializeField] private float maxForwardDistance = 6f;
        [SerializeField] private float maxGroundProbeDistance = 25f;
        [SerializeField] private float maxMouseRayDistance = 30f;
        [SerializeField, Range(0f,1f)] private float minGroundNormalY = 0.4f;
        [SerializeField] private float yMaxStepPerSecond = 8f;
        [SerializeField] private float heightScrollSpeed = 4f;
        [SerializeField] private float heightKeySpeed = 2f;
        [SerializeField] private float minHeightOffset = -5f;
        [SerializeField] private float maxHeightOffset = 5f;

        // Multi-selection support
        private HashSet<Transform> selectedObjects = new HashSet<Transform>();
        private Transform primarySelected; // The "leader" token for group drag
        private Dictionary<Transform, Vector3> dragOffsets = new Dictionary<Transform, Vector3>();

        // Legacy single-selection (kept for compatibility, now points to primarySelected)
        private Transform selectedObject;
        private Rigidbody selectedRigidbody;

        // Drag state per token
        private class DragState
        {
            public Rigidbody rb;
            public bool wasKinematic;
            public bool hadGravity;
        }
        private Dictionary<Transform, DragState> dragStates = new Dictionary<Transform, DragState>();

        private bool isDragging = false;
        private bool isLocked = false;
        private float currentDistance;
        private Quaternion targetRotation;
        private bool wasKinematic;
        private bool hadGravity;
        public bool InputLocked { get; set; } = false;

        private Stack<UndoAction> undoStack = new();
        private GameObject copiedObject;
        
        // Network throttling
        private float lastNetworkMoveTime = 0f;
        private float networkMoveInterval = 0.02f;
        private Vector3 lastValidSurfacePoint = Vector3.zero;
        private float pivotToBottomOffset = 0f;
        private float tokenHalfHeight = 0f;
        private Plane dragPlane;
        private bool dragPlaneActive = false;
        private float dragHeightOffset = 0f;

        private void Start()
        {
            if (mainCamera == null)
            {
                if (Nexus.CameraManager.Instance != null && Nexus.CameraManager.Instance.MainCamera != null)
                    mainCamera = Nexus.CameraManager.Instance.MainCamera;
                else if (Camera.main != null)
                    mainCamera = Camera.main;
            }
        }

        private bool TrySelectUnderMouse()
        {
            if (mainCamera == null) return false;
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit[] hits = Physics.RaycastAll(ray, 500f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                var tokenSetup = hits[i].transform.GetComponentInParent<TokenSetup>();
                if (tokenSetup != null)
                {
                    Select(tokenSetup.transform);
                    return true;
                }
            }
            return false;
        }

        // External helpers: allow other systems to push undo entries
        public void PushMoveUndo(GameObject target, Vector3 oldPos, Quaternion oldRot, Vector3 newPos, Quaternion newRot)
        {
            if (target == null) return;
            undoStack.Push(new UndoAction
            {
                actionType = UndoActionType.Move,
                targetObject = target,
                oldPosition = oldPos,
                newPosition = newPos,
                oldRotation = oldRot,
                newRotation = newRot
            });
        }

        public void PushRotateUndo(GameObject target, Quaternion oldRot, Quaternion newRot)
        {
            if (target == null) return;
            undoStack.Push(new UndoAction
            {
                actionType = UndoActionType.Rotate,
                targetObject = target,
                oldRotation = oldRot,
                newRotation = newRot
            });
        }

        private Nexus.Networking.NetworkPlayer boundLocalPlayer;
        public void BindLocalPlayer(Nexus.Networking.NetworkPlayer player) { boundLocalPlayer = player; }

        private void Update()
        {
            if (GetActive() != this) return;
            // Always ensure we have the correct camera, preferring the bound local player's camera
            if (boundLocalPlayer != null && boundLocalPlayer.isLocalPlayer && boundLocalPlayer.PlayerCamera != null && boundLocalPlayer.PlayerCamera.isActiveAndEnabled)
            {
                mainCamera = boundLocalPlayer.PlayerCamera;
            }
            else if (Nexus.CameraManager.Instance != null)
            {
                var cam = Nexus.CameraManager.Instance.MainCamera;
                if (cam != null) mainCamera = cam;
            }
            else
            {
                var cam = Camera.main;
                if (cam != null) mainCamera = cam;
            }

            // Only selection/movement require camera; shortcuts should always work
            if (mainCamera != null)
            {
                HandleSelection();
                HandleObjectManipulation();
                if (isDragging) MoveSelectedObject();
            }
            HandleUndo();
            HandleCopyPaste();
            HandleDelete();
        }

        private static bool IsCtrlPressed()
        {
            return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) ||
                   Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand);
        }


        // ===============================
        // ======== SELECTION ============
        // ===============================
        private void HandleSelection()
        {
            bool shiftPressed = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            if (Input.GetMouseButtonDown(0))
            {
                // Disable TokenDraggable to prevent conflicts
                DisableTokenDraggableOnClick();

                Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
                RaycastHit[] hits = Physics.RaycastAll(ray, 500f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                Transform bestTarget = null;
                bool bestIsToken = false;
                bool bestIsMovable = false;
                RaycastHit bestHit = new RaycastHit();

                // Prefer tokens first
                for (int i = 0; i < hits.Length && bestTarget == null; i++)
                {
                    var tokenSetup = hits[i].transform.GetComponentInParent<TokenSetup>();
                    if (tokenSetup != null)
                    {
                        bestTarget = tokenSetup.transform;
                        bestIsToken = true;
                        bestHit = hits[i];
                        break;
                    }
                }
                // Tokens only: do not select generic Movables

                if (bestTarget != null)
                {
                    // Multi-selection with Shift
                    if (shiftPressed)
                    {
                        // Toggle selection
                        if (selectedObjects.Contains(bestTarget))
                        {
                            // Remove from selection
                            selectedObjects.Remove(bestTarget);
                            if (primarySelected == bestTarget)
                            {
                                // Choose new primary from remaining selection
                                primarySelected = selectedObjects.Count > 0 ?
                                    System.Linq.Enumerable.First(selectedObjects) : null;
                            }
                            selectedObject = primarySelected;
                            Debug.Log($"Deselected: {bestTarget.name} (Remaining: {selectedObjects.Count})");
                        }
                        else
                        {
                            // Add to selection
                            selectedObjects.Add(bestTarget);
                            primarySelected = bestTarget; // Last clicked becomes primary
                            selectedObject = primarySelected;
                            Select(bestTarget);
                            Debug.Log($"Added to selection: {bestTarget.name} (Total: {selectedObjects.Count})");
                        }

                        // Don't start dragging when shift-clicking (just selecting)
                    }
                    else
                    {
                        // Normal click: check if clicking on already selected token
                        bool clickedOnSelected = selectedObjects.Contains(bestTarget);

                        if (!clickedOnSelected)
                        {
                            // Clicking on new token: clear selection and select only this one
                            Deselect();
                            selectedObjects.Add(bestTarget);
                            primarySelected = bestTarget;
                            selectedObject = primarySelected;
                            Select(bestTarget);
                            Debug.Log($"Selected: {bestTarget.name} (Token: {bestIsToken}, Movable: {bestIsMovable})");
                        }

                        // Start dragging (works for single or group)
                        SaveStateAndStartDragging(bestHit);
                    }
                }
                else
                {
                    // Clicked empty space
                    if (!shiftPressed)
                    {
                        Deselect();
                    }
                }
            }

            if (Input.GetMouseButtonUp(0))
            {
                if (isDragging)
                {
                    StopDragging();
                }
            }
        }

        private void DisableTokenDraggableOnClick()
        {
            // Disable all TokenDraggable components to prevent conflicts
            var allDraggables = Object.FindObjectsOfType<TokenDraggable>();
            foreach (var draggable in allDraggables)
            {
                draggable.enabled = false;
            }
        }

        // ===============================
        // ======= MOVEMENT / ROTATE =====
        // ===============================
        private void HandleObjectManipulation()
        {
            if (selectedObject == null)
                return;

            if (Input.GetKeyDown(KeyCode.L))
            {
                var netToken = selectedObject != null ? selectedObject.GetComponentInParent<Nexus.Networking.NetworkedToken>() : null;
                if (netToken != null)
                {
                    netToken.CmdSetLocked(!netToken.IsLocked);
                }
            }

            

            
        }

        private void MoveSelectedObject()
        {
            if (primarySelected == null || selectedObjects.Count == 0)
                return;

            // Calculate leader target position using drag plane + ground detection
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            Vector3 leaderTarget;
            bool foundGround = false;

            // Use drag plane for XZ position (prevents token from coming towards camera)
            if (dragPlaneActive && dragPlane.Raycast(ray, out float enter))
            {
                Vector3 planeHitPoint = ray.GetPoint(enter);

                // Do a downward raycast from the plane hit to find actual ground
                // Start probe above the plane hit point
                Vector3 probeStart = new Vector3(planeHitPoint.x, planeHitPoint.y + 10f, planeHitPoint.z);

                if (Physics.Raycast(probeStart, Vector3.down, out RaycastHit downHit, maxGroundProbeDistance, moveSurfaceMask, QueryTriggerInteraction.Ignore))
                {
                    // Check if we hit a token (skip it)
                    bool hitToken = downHit.transform.GetComponentInParent<TokenSetup>() != null;

                    if (!hitToken && downHit.normal.y >= minGroundNormalY)
                    {
                        // Found valid ground, use it
                        leaderTarget = downHit.point + Vector3.up * (tokenHalfHeight + groundSnapOffset);
                        foundGround = true;
                    }
                    else
                    {
                        // Hit token or steep surface, use XZ from plane but keep current Y
                        leaderTarget = new Vector3(planeHitPoint.x, primarySelected.position.y, planeHitPoint.z);
                        foundGround = true;
                    }
                }
                else
                {
                    // No ground found below plane, keep current Y
                    leaderTarget = new Vector3(planeHitPoint.x, primarySelected.position.y, planeHitPoint.z);
                    foundGround = true;
                }
            }
            else
            {
                // Fallback: use fixed distance from camera
                Vector3 mouseScreenPos = new Vector3(Input.mousePosition.x, Input.mousePosition.y, currentDistance);
                Vector3 screenWorldPoint = mainCamera.ScreenToWorldPoint(mouseScreenPos);

                // Try to find ground below this point
                Vector3 probeStart = new Vector3(screenWorldPoint.x, screenWorldPoint.y + 10f, screenWorldPoint.z);

                if (Physics.Raycast(probeStart, Vector3.down, out RaycastHit downHit, maxGroundProbeDistance, moveSurfaceMask, QueryTriggerInteraction.Ignore))
                {
                    // Check if we hit a token (skip it)
                    bool hitToken = downHit.transform.GetComponentInParent<TokenSetup>() != null;

                    if (!hitToken && downHit.normal.y >= minGroundNormalY)
                    {
                        leaderTarget = downHit.point + Vector3.up * (tokenHalfHeight + groundSnapOffset);
                        foundGround = true;
                    }
                    else
                    {
                        leaderTarget = new Vector3(screenWorldPoint.x, primarySelected.position.y, screenWorldPoint.z);
                        foundGround = true;
                    }
                }
                else
                {
                    // No ground found, use screen point with current Y
                    leaderTarget = new Vector3(screenWorldPoint.x, primarySelected.position.y, screenWorldPoint.z);
                }
            }

            // Move all selected tokens maintaining formation
            foreach (var token in selectedObjects)
            {
                if (token == null) continue;

                // Calculate target position with offset
                Vector3 offset = dragOffsets.ContainsKey(token) ? dragOffsets[token] : Vector3.zero;
                Vector3 targetPos = leaderTarget + offset;

                // For tokens with offset, also check ground height at their position
                if (offset.sqrMagnitude > 0.01f && foundGround)
                {
                    Vector3 tokenProbeStart = new Vector3(targetPos.x, targetPos.y + 10f, targetPos.z);
                    if (Physics.Raycast(tokenProbeStart, Vector3.down, out RaycastHit tokenGroundHit, maxGroundProbeDistance, moveSurfaceMask, QueryTriggerInteraction.Ignore))
                    {
                        // Check if we hit a token (skip it)
                        bool hitToken = tokenGroundHit.transform.GetComponentInParent<TokenSetup>() != null;

                        if (!hitToken && tokenGroundHit.normal.y >= minGroundNormalY)
                        {
                            // Adjust Y to match ground at this token's position
                            float tokenHeight = ComputeHalfHeight(token);
                            targetPos.y = tokenGroundHit.point.y + tokenHeight + groundSnapOffset;
                        }
                    }
                }

                // Use rigidbody if available
                if (dragStates.ContainsKey(token) && dragStates[token].rb != null)
                {
                    dragStates[token].rb.MovePosition(targetPos);
                }
                else
                {
                    token.position = targetPos;
                }
            }

            // Send periodic network updates during drag to prevent flickering
            bool networkActive = Mirror.NetworkServer.active || Mirror.NetworkClient.isConnected;
            if (networkActive && Time.time - lastNetworkMoveTime > networkMoveInterval)
            {
                foreach (var token in selectedObjects)
                {
                    if (token == null) continue;

                    var netToken = token.GetComponentInParent<Nexus.Networking.NetworkedToken>();
                    if (netToken != null && netToken.netIdentity != null)
                    {
                        netToken.CmdUpdatePosition(token.position);
                    }
                    else
                    {
                        var netMovable = token.GetComponentInParent<Nexus.Networking.NetworkedMovable>();
                        if (netMovable != null && netMovable.netIdentity != null)
                        {
                            netMovable.CmdUpdatePosition(token.position);
                        }
                    }
                }
                lastNetworkMoveTime = Time.time;
            }

            // Update legacy fields for compatibility
            selectedObject = primarySelected;
        }

        // ===============================
        // ========= UNDO (CTRL+Z) =======
        // ===============================
        private void HandleUndo()
        {
            if (IsCtrlPressed() && Input.GetKeyDown(KeyCode.Z))
            {
                if (undoStack.Count > 0)
                {
                    UndoAction action = undoStack.Pop();
                    action.Undo();
                }
            }
        }

        // ===============================
        // ===== COPY/PASTE (CTRL+C/V) ===
        // ===============================
        private void HandleCopyPaste()
        {
            // Copy (Ctrl+C)
            if (IsCtrlPressed() && Input.GetKeyDown(KeyCode.C))
            {
                if (selectedObjects.Count == 0)
                {
                    TrySelectUnderMouse();
                }
                if (primarySelected != null)
                {
                    // For now, only copy the primary selected object
                    // TODO: Support copying multiple objects
                    copiedObject = primarySelected.gameObject;
                    Debug.Log("Copied: " + copiedObject.name);
                }
            }

            // Paste (Ctrl+V)
            if (IsCtrlPressed() && Input.GetKeyDown(KeyCode.V))
            {
                if (copiedObject != null)
                {
                    // Check if this is a networked token
                    var networkedToken = copiedObject.GetComponent<Nexus.Networking.NetworkedToken>();
                    if (networkedToken != null && (Mirror.NetworkServer.active || Mirror.NetworkClient.isConnected))
                    {
                        // Calculate spawn position on client side using local camera
                        Vector3 spawnPos = FindValidSpawnPosition();

                        // Use client->server command via local NetworkPlayer
                        var localPlayerIdentity = Mirror.NetworkClient.localPlayer;
                        var localPlayer = localPlayerIdentity != null
                            ? localPlayerIdentity.GetComponent<Nexus.Networking.NetworkPlayer>()
                            : FindLocalNetworkPlayer();
                        if (localPlayer != null)
                        {
                            string prefabName = SanitizePrefabName(copiedObject.name);
                            localPlayer.CmdSpawnTokenByName(prefabName, spawnPos);
                            Debug.Log("Network spawned: " + prefabName);
                        }
                    }
                    else
                    {
                        // Original local spawning for non-networked objects
                        Vector3 spawnPos = FindValidSpawnPosition();
                        GameObject newObj = Instantiate(copiedObject, spawnPos, copiedObject.transform.rotation);

                        undoStack.Push(new UndoAction
                        {
                            actionType = UndoActionType.Create,
                            targetObject = newObj
                        });

                        Debug.Log("Pasted: " + newObj.name);
                    }
                }
            }
        }

        private Vector3 FindValidSpawnPosition()
        {
            Camera cam = mainCamera;
            if (cam == null)
                cam = Camera.main;
            if (cam == null)
                return transform.position;

            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            float clampDistance = Mathf.Min(maxMouseRayDistance, 15f);
            if (Physics.Raycast(ray, out RaycastHit hit, clampDistance, moveSurfaceMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.normal.y >= minGroundNormalY)
                {
                    return hit.point;
                }
            }

            Vector3 forwardTarget = cam.transform.position + cam.transform.forward * clampDistance;
            Vector3 downStart = forwardTarget + Vector3.up * 10f;
            if (Physics.Raycast(downStart, Vector3.down, out RaycastHit downHit, 50f, moveSurfaceMask, QueryTriggerInteraction.Ignore))
            {
                if (downHit.normal.y >= minGroundNormalY)
                {
                    return downHit.point;
                }
            }
            return cam.transform.position + cam.transform.forward * 3f;
        }

        // ===============================
        // ======== DELETE (DEL) =========
        // ===============================
        private void HandleDelete()
        {
            if (Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.Backspace))
            {
                if (selectedObjects.Count == 0)
                {
                    TrySelectUnderMouse();
                }

                if (selectedObjects.Count > 0)
                {
                    // Delete all selected objects
                    var objectsToDelete = new System.Collections.Generic.List<Transform>(selectedObjects);

                    foreach (var obj in objectsToDelete)
                    {
                        if (obj == null) continue;

                        GameObject objToDelete = obj.gameObject;
                        Vector3 oldPos = objToDelete.transform.position;
                        Quaternion oldRot = objToDelete.transform.rotation;

                        undoStack.Push(new UndoAction
                        {
                            actionType = UndoActionType.Delete,
                            targetObject = objToDelete,
                            oldPosition = oldPos,
                            oldRotation = oldRot
                        });

                        objToDelete.SetActive(false);
                    }

                    Deselect();
                }
            }
        }

        // ===============================
        // ========= UTILITIES ===========
        // ===============================
        public void Select(Transform obj)
        {
            // Always prefer the Token root as the selected object
            Transform root = obj;
            var token = obj.GetComponentInParent<TokenSetup>();
            if (token != null) root = token.transform;

            // Update legacy fields for compatibility
            selectedObject = root;
            // Cache a rigidbody if it belongs to the same token hierarchy, but do not change selectedObject to it
            Rigidbody rb = root.GetComponentInChildren<Rigidbody>();
            selectedRigidbody = (rb != null && (rb.transform == root || rb.transform.IsChildOf(root))) ? rb : null;
            targetRotation = root.rotation;
        }

        private void Deselect()
        {
            if (isDragging)
                StopDragging();

            selectedObjects.Clear();
            primarySelected = null;
            selectedObject = null;
            selectedRigidbody = null;
            isDragging = false;
            isLocked = false;
        }

        private void SaveStateAndStartDragging(RaycastHit hit)
        {
            if (primarySelected == null || selectedObjects.Count == 0)
                return;

            isDragging = true;
            dragOffsets.Clear();
            dragStates.Clear();

            // Use camera-forward depth so ScreenToWorldPoint doesn't jump to camera height
            currentDistance = Mathf.Max(0.5f, Vector3.Dot(primarySelected.position - mainCamera.transform.position, mainCamera.transform.forward));
            pivotToBottomOffset = ComputePivotToBottom(primarySelected);
            tokenHalfHeight = ComputeHalfHeight(primarySelected);
            // Build a stable drag plane from initial hit
            Vector3 planeNormal = hit.normal.y >= minGroundNormalY ? hit.normal : Vector3.up;
            dragPlane = new Plane(planeNormal, hit.point);
            dragPlaneActive = true;
            // Initialize free height offset so there is no jump when starting the drag
            dragHeightOffset = primarySelected.position.y - (hit.point.y + tokenHalfHeight + groundSnapOffset);

            // Calculate offsets and save states for all selected tokens
            foreach (var token in selectedObjects)
            {
                if (token == null) continue;

                // Calculate offset from primary
                Vector3 offset = token.position - primarySelected.position;
                dragOffsets[token] = offset;

                // Save physics state
                var rb = token.GetComponentInChildren<Rigidbody>();
                if (rb != null && (rb.transform == token || rb.transform.IsChildOf(token)))
                {
                    var state = new DragState
                    {
                        rb = rb,
                        wasKinematic = rb.isKinematic,
                        hadGravity = rb.useGravity
                    };
                    dragStates[token] = state;

                    // Make rigidbody kinematic during drag for precise control
                    rb.velocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.isKinematic = true;
                    rb.useGravity = false;
                }

                // Network: begin drag for each token
                var identity = token.GetComponentInParent<Mirror.NetworkIdentity>();
                bool networkActive = Mirror.NetworkServer.active || Mirror.NetworkClient.isConnected;
                bool identitySpawned = identity != null && identity.netId != 0 && (identity.isClient || identity.isServer);

                if (identitySpawned && networkActive)
                {
                    var netToken = token.GetComponentInParent<Nexus.Networking.NetworkedToken>();
                    if (netToken != null)
                    {
                        if (netToken.netIdentity == null)
                        {
                            Debug.LogWarning($"Skip CmdBeginDrag on {netToken.name} because NetworkedToken.netIdentity is null (object not fully spawned?)");
                        }
                        else
                        {
                            netToken.SetLocalDragOwner(true);
                            netToken.CmdBeginDrag();
                        }
                    }
                    else
                    {
                        var netMovable = token.GetComponentInParent<Nexus.Networking.NetworkedMovable>();
                        if (netMovable != null)
                        {
                            if (netMovable.netIdentity == null)
                            {
                                Debug.LogWarning($"Skip CmdBeginDrag on {netMovable.name} because NetworkedMovable.netIdentity is null (object not fully spawned?)");
                            }
                            else
                            {
                                netMovable.SetLocalDragOwner(true);
                                netMovable.CmdBeginDrag();
                            }
                        }
                    }
                }
            }

            // Push undo for group move
            if (selectedObjects.Count == 1)
            {
                // Single object: use legacy undo
                var token = primarySelected;
                undoStack.Push(new UndoAction
                {
                    actionType = UndoActionType.Move,
                    targetObject = token.gameObject,
                    oldPosition = token.position,
                    oldRotation = token.rotation
                });
            }
            else
            {
                // Multiple objects: use multi-move undo
                var entries = new System.Collections.Generic.List<MultiMoveEntry>();
                foreach (var token in selectedObjects)
                {
                    if (token == null) continue;
                    entries.Add(new MultiMoveEntry
                    {
                        target = token,
                        oldPos = token.position,
                        oldRot = token.rotation,
                        newPos = token.position, // Will be updated on drag end
                        newRot = token.rotation
                    });
                }
                undoStack.Push(new UndoAction
                {
                    actionType = UndoActionType.MultiMove,
                    multiMoveEntries = entries
                });
            }

            // Update legacy fields for compatibility
            selectedObject = primarySelected;
            selectedRigidbody = dragStates.ContainsKey(primarySelected) ? dragStates[primarySelected].rb : null;
            if (selectedRigidbody != null)
            {
                wasKinematic = dragStates[primarySelected].wasKinematic;
                hadGravity = dragStates[primarySelected].hadGravity;
            }
        }

        private void StopDragging()
        {
            isDragging = false;

            bool networkActive = Mirror.NetworkServer.active || Mirror.NetworkClient.isConnected;

            // Restore physics and send network updates for all selected tokens
            foreach (var token in selectedObjects)
            {
                if (token == null) continue;

                // Restore physics state
                if (dragStates.ContainsKey(token))
                {
                    var state = dragStates[token];
                    if (state.rb != null)
                    {
                        state.rb.isKinematic = state.wasKinematic;
                        state.rb.useGravity = state.hadGravity;
                        state.rb.velocity = Vector3.zero;
                        state.rb.angularVelocity = Vector3.zero;
                    }
                }

                // Network: end drag for each token
                var identity = token.GetComponentInParent<Mirror.NetworkIdentity>();
                bool identitySpawned = identity != null && identity.netId != 0 && (identity.isClient || identity.isServer);

                var netToken = token.GetComponentInParent<Nexus.Networking.NetworkedToken>();
                if (netToken != null && netToken.netIdentity == null)
                {
                    Debug.LogWarning($"Skip CmdEndDragFinal on {netToken.name} because NetworkedToken.netIdentity is null (object not fully spawned?)");
                }
                else if (netToken != null && networkActive && identitySpawned)
                {
                    var root = identity != null ? identity.transform : token;
                    netToken.CmdEndDragFinal(root.position, root.rotation);
                }
                else if (networkActive && identitySpawned)
                {
                    var netMovable = token.GetComponentInParent<Nexus.Networking.NetworkedMovable>();
                    if (netMovable != null)
                    {
                        var root = identity != null ? identity.transform : token;
                        netMovable.CmdEndDragFinal(root.position, root.rotation);
                    }
                }
            }

            // Update undo stack with final positions for multi-move
            if (undoStack.Count > 0)
            {
                var lastAction = undoStack.Peek();
                if (lastAction.actionType == UndoActionType.MultiMove && lastAction.multiMoveEntries != null)
                {
                    foreach (var entry in lastAction.multiMoveEntries)
                    {
                        if (entry.target != null)
                        {
                            entry.newPos = entry.target.position;
                            entry.newRot = entry.target.rotation;
                        }
                    }
                }
                else if (lastAction.actionType == UndoActionType.Move && lastAction.targetObject != null)
                {
                    lastAction.newPosition = lastAction.targetObject.transform.position;
                    lastAction.newRotation = lastAction.targetObject.transform.rotation;
                }
            }

            // Clear drag state
            dragStates.Clear();
            dragOffsets.Clear();
            dragPlaneActive = false;
            dragHeightOffset = 0f;

            // Update legacy fields for compatibility
            selectedObject = primarySelected;
            selectedRigidbody = null;
        }



        private Nexus.Networking.NetworkPlayer FindLocalNetworkPlayer()
        {
            var players = Object.FindObjectsOfType<Nexus.Networking.NetworkPlayer>();
            foreach (var p in players)
            {
                if (p.isLocalPlayer) return p;
            }
            return null;
        }

        private string SanitizePrefabName(string instanceName)
        {
            if (string.IsNullOrEmpty(instanceName)) return instanceName;
            const string cloneSuffix = "(Clone)";
            if (instanceName.EndsWith(cloneSuffix))
            {
                return instanceName.Substring(0, instanceName.Length - cloneSuffix.Length).Trim();
            }
            return instanceName;
        }

        private float ComputePivotToBottom(Transform root)
        {
            var cols = root.GetComponentsInChildren<Collider>();
            if (cols == null || cols.Length == 0) return 0f;
            Bounds b = cols[0].bounds;
            for (int i = 1; i < cols.Length; i++) b.Encapsulate(cols[i].bounds);
            // Return bottomY - pivotY (usually negative when pivot is above bottom)
            return b.min.y - root.position.y;
        }

        private float ComputeHalfHeight(Transform root)
        {
            var cols = root.GetComponentsInChildren<Collider>();
            if (cols == null || cols.Length == 0) return 0f;
            Bounds b = cols[0].bounds;
            for (int i = 1; i < cols.Length; i++) b.Encapsulate(cols[i].bounds);
            return b.extents.y;
        }
    }

    // ===============================
    // ======= UNDO SYSTEM ===========
    // ===============================
    public enum UndoActionType
    {
        Move,
        Rotate,
        Create,
        Delete,
        MultiMove
    }

    public class MultiMoveEntry
    {
        public Transform target;
        public Vector3 oldPos;
        public Quaternion oldRot;
        public Vector3 newPos;
        public Quaternion newRot;
    }

    public class UndoAction
    {
        public UndoActionType actionType;
        public GameObject targetObject;
        public Vector3 oldPosition;
        public Vector3 newPosition;
        public Quaternion oldRotation;
        public Quaternion newRotation;

        // For multi-move
        public System.Collections.Generic.List<MultiMoveEntry> multiMoveEntries;

        public void Undo()
        {
            switch (actionType)
            {
                case UndoActionType.Move:
                    if (targetObject != null)
                    {
                        targetObject.transform.position = oldPosition;
                        targetObject.transform.rotation = oldRotation;
                    }
                    break;

                case UndoActionType.Rotate:
                    if (targetObject != null)
                    {
                        targetObject.transform.rotation = oldRotation;
                    }
                    break;

                case UndoActionType.Create:
                    if (targetObject != null)
                    {
                        Object.Destroy(targetObject);
                    }
                    break;

                case UndoActionType.Delete:
                    if (targetObject != null)
                    {
                        targetObject.SetActive(true);
                        targetObject.transform.position = oldPosition;
                        targetObject.transform.rotation = oldRotation;
                    }
                    break;

                case UndoActionType.MultiMove:
                    if (multiMoveEntries != null)
                    {
                        foreach (var entry in multiMoveEntries)
                        {
                            if (entry.target != null)
                            {
                                entry.target.position = entry.oldPos;
                                entry.target.rotation = entry.oldRot;
                            }
                        }
                    }
                    break;
            }
        }
    }
}