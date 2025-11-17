using UnityEngine;

/*Script to disable lighting and shadows 
when moving away at a set distance
MODIFICADO: Encontra a câmera automaticamente*/
namespace Nexus
{
    public class LightCulling : MonoBehaviour
    {
        [Header("Camera Settings")]
        [Tooltip("Nome exato da câmera (deixe vazio para buscar automaticamente)")]
        [SerializeField] private string cameraName = "Controller Camera";
        
        [Tooltip("Se não encontrar por nome, usar tag (deixe vazio para não usar)")]
        [SerializeField] private string cameraTag = "MainCamera";
        
        [Header("Culling Distances")]
        [SerializeField] private float shadowCullingDistance = 15f;
        [SerializeField] private float lightCullingDistance = 30f;
        
        [Header("Shadow Settings")]
        public bool enableShadows = false;
        
        [Header("Performance")]
        [Tooltip("Atualizar a cada X frames (1 = todo frame, 5 = a cada 5 frames)")]
        [Range(1, 10)]
        [SerializeField] private int updateFrequency = 3;
        
        [Header("Debug")]
        [Tooltip("Mostrar warnings se não encontrar a câmera?")]
        [SerializeField] private bool showDebugWarnings = false;
        
        // Cache
        private static GameObject cachedPlayerCamera; // Estático = compartilhado entre todas as 146 luzes
        private Light _light;
        private int frameCounter;
        private float randomOffset;
        private static bool cameraSearchPerformed = false;

        private void Awake()
        {
            _light = GetComponent<Light>();
            
            // Offset aleatório para não atualizar todas as 146 luzes no mesmo frame
            randomOffset = Random.Range(0, updateFrequency);
            frameCounter = Mathf.RoundToInt(randomOffset);
        }

        private void Start()
        {
            // Tenta encontrar a câmera uma vez (compartilhada entre todas as luzes)
            if (cachedPlayerCamera == null && !cameraSearchPerformed)
            {
                FindPlayerCamera();
                cameraSearchPerformed = true;
            }
        }

        private void Update()
        {
            // Otimização: não atualiza todo frame
            frameCounter++;
            if (frameCounter < updateFrequency)
                return;
            
            frameCounter = 0;
            
            // Se a câmera foi destruída ou ainda não foi encontrada, tenta novamente
            if (cachedPlayerCamera == null)
            {
                FindPlayerCamera();
                
                // Se ainda não encontrou, desabilita a luz e retorna
                if (cachedPlayerCamera == null)
                {
                    _light.enabled = false;
                    return;
                }
            }

            // Calcula a distância entre a câmera e a luz
            float cameraDistance = Vector3.Distance(cachedPlayerCamera.transform.position, transform.position);

            // Gerenciamento de sombras
            if (cameraDistance <= shadowCullingDistance && enableShadows)
            {
                _light.shadows = LightShadows.Soft;
            }
            else
            {
                _light.shadows = LightShadows.None;
            }

            // Gerenciamento de luz
            if (cameraDistance <= lightCullingDistance)
            {
                _light.enabled = true;
            }
            else
            {
                _light.enabled = false;
            }
        }

        /// <summary>
        /// Encontra a câmera do jogador automaticamente usando múltiplos métodos
        /// </summary>
        private void FindPlayerCamera()
        {
            // Método 1: Busca por nome exato (mais específico)
            if (!string.IsNullOrEmpty(cameraName))
            {
                GameObject foundByName = GameObject.Find(cameraName);
                if (foundByName != null)
                {
                    cachedPlayerCamera = foundByName;
                    if (showDebugWarnings)
                        Debug.Log($"[LightCulling] Câmera encontrada por nome: {cameraName}");
                    return;
                }
            }

            // Método 2: Busca por tag
            if (!string.IsNullOrEmpty(cameraTag))
            {
                GameObject foundByTag = GameObject.FindGameObjectWithTag(cameraTag);
                if (foundByTag != null)
                {
                    cachedPlayerCamera = foundByTag;
                    if (showDebugWarnings)
                        Debug.Log($"[LightCulling] Câmera encontrada por tag: {cameraTag}");
                    return;
                }
            }

            // Método 3: Usa Camera.main (câmera marcada como MainCamera)
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                cachedPlayerCamera = mainCam.gameObject;
                if (showDebugWarnings)
                    Debug.Log($"[LightCulling] Câmera encontrada via Camera.main: {mainCam.name}");
                return;
            }

            // Método 4: Procura qualquer câmera ativa (último recurso)
            Camera anyCam = FindObjectOfType<Camera>();
            if (anyCam != null)
            {
                cachedPlayerCamera = anyCam.gameObject;
                if (showDebugWarnings)
                    Debug.Log($"[LightCulling] Câmera encontrada via FindObjectOfType: {anyCam.name}");
                return;
            }

            // Se chegou aqui, não encontrou nenhuma câmera
            if (showDebugWarnings)
            {
                Debug.LogWarning($"[LightCulling] Nenhuma câmera encontrada! Verifique se:\n" +
                                 $"- Existe uma câmera com o nome '{cameraName}'\n" +
                                 $"- Ou uma câmera com a tag '{cameraTag}'\n" +
                                 $"- Ou uma Camera.main na cena");
            }
        }

        /// <summary>
        /// Reseta o cache da câmera. Útil quando trocar de cena.
        /// Chame este método estático quando carregar uma nova SubScene.
        /// </summary>
        public static void ResetCameraCache()
        {
            cachedPlayerCamera = null;
            cameraSearchPerformed = false;
        }

        /// <summary>
        /// Define manualmente a câmera (útil para casos específicos)
        /// </summary>
        public static void SetPlayerCamera(GameObject camera)
        {
            cachedPlayerCamera = camera;
            cameraSearchPerformed = true;
        }

        // Opcional: Desenha gizmos no editor para visualizar as distâncias
        private void OnDrawGizmosSelected()
        {
            // Esfera amarela = distância de sombra
            Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, shadowCullingDistance);

            // Esfera vermelha = distância de luz
            Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, lightCullingDistance);
        }
    }
}