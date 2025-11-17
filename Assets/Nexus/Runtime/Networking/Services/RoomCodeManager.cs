using System;
using System.Text;
using UnityEngine;
using Mirror;

namespace Nexus.Networking
{
    /// <summary>
    /// Manages room code generation and validation for peer-to-peer multiplayer
    /// </summary>
    public class RoomCodeManager : MonoBehaviour
    {
        private static RoomCodeManager _instance;
        public static RoomCodeManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<RoomCodeManager>();
                }
                return _instance;
            }
        }

        [Header("Room Code Settings")]
        [SerializeField] private int codeLength = 6;
        
        private string currentRoomCode;
        
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// Generates a random room code
        /// </summary>
        public string GenerateRoomCode()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            StringBuilder code = new StringBuilder();
            System.Random random = new System.Random();

            for (int i = 0; i < codeLength; i++)
            {
                code.Append(chars[random.Next(chars.Length)]);
            }

            currentRoomCode = code.ToString();
            Debug.LogError($"Generated Room Code: {currentRoomCode}");
            return currentRoomCode;
        }

        /// <summary>
        /// Validates a room code format
        /// </summary>
        public bool ValidateRoomCode(string code)
        {
            if (string.IsNullOrEmpty(code))
                return false;

            if (code.Length != codeLength)
                return false;

            foreach (char c in code)
            {
                if (!char.IsLetterOrDigit(c))
                    return false;
            }

            return true;
        }

        public string GetCurrentRoomCode()
        {
            return currentRoomCode;
        }

        public void SetCurrentRoomCode(string code)
        {
            currentRoomCode = code.ToUpper();
        }
    }
}
