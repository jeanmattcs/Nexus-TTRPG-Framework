using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Nexus.Networking;
using Mirror;
using Tenkoku.Core;

namespace Nexus
{
    public class EnviroUIController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject uiPanel;
        [SerializeField] private Slider timeSlider;
        [SerializeField] private TextMeshProUGUI timeText;
        [SerializeField] private TMP_Dropdown weatherDropdown;
        [SerializeField] private Slider timeSpeedSlider;
        [SerializeField] private TextMeshProUGUI timeSpeedText;
        [SerializeField] private Toggle pauseTimeToggle;

        [Header("Settings")]
        [SerializeField] private KeyCode toggleKey = KeyCode.P;
        [SerializeField] private float minTimeSpeed = 0.1f;
        [SerializeField] private float maxTimeSpeed = 100f;

        private PlayerController playerController;
        private TabletopManager tabletopManager;
        private bool isUIActive = false;
        private bool isSetupComplete = false;
        private NetworkedWeather netWeather;

        private NetworkedWeather GetNetWeather()
        {
            if (netWeather == null)
                netWeather = FindObjectOfType<NetworkedWeather>();
            return netWeather;
        }

        private void Start()
        {
            playerController = FindObjectOfType<PlayerController>();
            tabletopManager = FindObjectOfType<TabletopManager>();

            if (uiPanel != null)
                uiPanel.SetActive(false);

            // Try initial setup
            SetupUI();
        }

        private void LateStart()
        {
            // Retry setup if it failed initially
            if (!isSetupComplete)
            {
                SetupUI();
            }
        }

        private void Update()
        {
            // Retry setup if Tenkoku wasn't ready during Start
            if (!isSetupComplete && FindObjectOfType<TenkokuModule>() != null)
            {
                SetupUI();
            }

            if (Input.GetKeyDown(toggleKey))
            {
                ToggleUI();
            }

            if (isUIActive)
            {
                UpdateTimeDisplay();
            }
        }

        private void SetupUI()
        {
            // Check if Tenkoku is available
            var ten = FindObjectOfType<TenkokuModule>();
            if (ten == null)
            {
                Debug.LogWarning("TenkokuModule not found yet. Will retry...");
                return;
            }

            if (netWeather == null)
                netWeather = FindObjectOfType<NetworkedWeather>();

            Debug.Log("Setting up Tenkoku UI...");

            // Time Slider (0-24 hours)
            if (timeSlider != null)
            {
                timeSlider.minValue = 0f;
                timeSlider.maxValue = 24f;
                timeSlider.value = ten.currentHour + (ten.currentMinute / 60f);
                timeSlider.onValueChanged.RemoveAllListeners();
                timeSlider.onValueChanged.AddListener(OnTimeChanged);
            }

            // Weather Dropdown (fixed list)
            if (weatherDropdown != null)
            {
                weatherDropdown.ClearOptions();
                List<string> weatherNames = new List<string> { "Clear", "Cloudy", "Rain", "Storm", "Snow" };
                weatherDropdown.AddOptions(weatherNames);
                weatherDropdown.value = GetCurrentWeatherIndex();
                weatherDropdown.onValueChanged.RemoveAllListeners();
                weatherDropdown.onValueChanged.AddListener(OnWeatherChanged);
            }

            // Time Speed Slider - hidden by default
            if (timeSpeedSlider != null)
            {
                timeSpeedSlider.minValue = minTimeSpeed;
                timeSpeedSlider.maxValue = maxTimeSpeed;
                timeSpeedSlider.value = 10f; // Default value
                timeSpeedSlider.onValueChanged.RemoveAllListeners();
                timeSpeedSlider.onValueChanged.AddListener(OnTimeSpeedChanged);
                if (timeSpeedSlider.transform.parent != null)
                    timeSpeedSlider.transform.parent.gameObject.SetActive(false);
            }

            // Pause Toggle
            if (pauseTimeToggle != null)
            {
                pauseTimeToggle.isOn = (!ten.autoTime) || ten.timeCompression <= 0.0001f;
                pauseTimeToggle.onValueChanged.RemoveAllListeners();
                pauseTimeToggle.onValueChanged.AddListener(OnPauseToggled);
            }

            isSetupComplete = true;
            Debug.Log("Tenkoku UI setup complete!");
        }

        private void ToggleUI()
        {
            isUIActive = !isUIActive;
            
            if (uiPanel != null)
                uiPanel.SetActive(isUIActive);

            // Lock/unlock controls
            if (playerController != null)
                playerController.InputLocked = isUIActive;
            
            if (tabletopManager != null)
                tabletopManager.InputLocked = isUIActive;

            // Cursor
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Update UI values when opening
            if (isUIActive)
            {
                var ten = FindObjectOfType<TenkokuModule>();
                if (ten != null)
                {
                    if (timeSlider != null)
                        timeSlider.value = ten.currentHour + (ten.currentMinute / 60f);
                    
                    if (weatherDropdown != null)
                        weatherDropdown.value = GetCurrentWeatherIndex();
                    
                    if (pauseTimeToggle != null)
                        pauseTimeToggle.isOn = (!ten.autoTime) || ten.timeCompression <= 0.0001f;
                }
            }
        }

        // ===============================
        // ======= TIME CONTROL ==========
        // ===============================
        private void OnTimeChanged(float value)
        {
            var nw = GetNetWeather();
            if (nw != null)
            {
                nw.CmdSetTimeOfDay(value);
            }
            else if (!NetworkServer.active && !NetworkClient.isConnected)
            {
                var ten = FindObjectOfType<TenkokuModule>();
                if (ten != null)
                {
                    SetTimeOfDay(ten, value);
                }
            }
        }

        private void OnTimeSpeedChanged(float value)
        {
            // Not implemented for now
            if (timeSpeedText != null)
                timeSpeedText.text = $"Speed: {value:F1}x (Not supported)";
        }

        private void OnPauseToggled(bool paused)
        {
            var nw = GetNetWeather();
            if (nw != null)
            {
                nw.CmdSetTimePaused(paused);
            }
            else if (!NetworkServer.active && !NetworkClient.isConnected)
            {
                var ten = FindObjectOfType<TenkokuModule>();
                if (ten != null)
                {
                    if (paused)
                    {
                        ten.autoTime = false;
                        ten.timeCompression = 0f;
                    }
                    else
                    {
                        ten.autoTime = true;
                    }
                }
            }
        }

        private void UpdateTimeDisplay()
        {
            var ten = FindObjectOfType<TenkokuModule>();
            if (ten != null && timeText != null)
            {
                int h = ten.currentHour;
                int m = ten.currentMinute;
                timeText.text = $"{h:00}:{m:00}";
            }
        }

        // ===============================
        // ====== WEATHER CONTROL ========
        // ===============================
        private void OnWeatherChanged(int index)
        {
            var nw = GetNetWeather();
            if (nw != null)
            {
                nw.CmdChangeWeather(index);
            }
            else if (!NetworkServer.active && !NetworkClient.isConnected)
            {
                var ten = FindObjectOfType<TenkokuModule>();
                if (ten != null)
                {
                    ApplyWeatherPreset(ten, index);
                    Debug.Log($"Weather changed to: {index}");
                }
            }
        }

        private int GetCurrentWeatherIndex()
        {
            var ten = FindObjectOfType<TenkokuModule>();
            if (ten == null) return 0;

            if (ten.weather_SnowAmt > 0.5f) return 4; // Snow
            if (ten.weather_lightning > 0.25f || (ten.weather_RainAmt > 0.7f && ten.weather_OvercastAmt > 0.8f)) return 3; // Storm
            if (ten.weather_RainAmt > 0.2f) return 2; // Rain
            if (ten.weather_OvercastAmt > 0.4f || ten.weather_cloudCumulusAmt > 0.5f) return 1; // Cloudy
            return 0; // Clear
        }

        // ===============================
        // ======= PUBLIC HELPERS ========
        // ===============================
        
        // Quick weather change methods you can call from other scripts
        public void ChangeWeatherByName(string weatherName)
        {
            if (string.IsNullOrEmpty(weatherName)) return;
            int idx = 0;
            switch (weatherName.ToLowerInvariant())
            {
                case "clear": idx = 0; break;
                case "cloudy": idx = 1; break;
                case "rain": idx = 2; break;
                case "storm": idx = 3; break;
                case "snow": idx = 4; break;
                default:
                    Debug.LogWarning($"Weather preset '{weatherName}' not recognized.");
                    return;
            }

            var nw = GetNetWeather();
            if (nw != null)
                nw.CmdChangeWeather(idx);
            else if (!NetworkServer.active && !NetworkClient.isConnected)
            {
                var ten = FindObjectOfType<TenkokuModule>();
                if (ten != null)
                    ApplyWeatherPreset(ten, idx);
            }

            if (weatherDropdown != null)
                weatherDropdown.value = idx;
        }

        public void SetTimeOfDay(int hour, int minute)
        {
            float tod = hour + (minute / 60f);
            var nw = GetNetWeather();
            if (nw != null)
            {
                nw.CmdSetTimeOfDay(tod);
            }
            else if (!NetworkServer.active && !NetworkClient.isConnected)
            {
                var ten = FindObjectOfType<TenkokuModule>();
                if (ten != null)
                    SetTimeOfDay(ten, tod);
            }
            
            if (timeSlider != null)
                timeSlider.value = tod;
        }

        // ===============================
        // ==== QUICK TIME BUTTONS =======
        // ===============================
        
        /// <summary>
        /// Dawn/Sunrise - 6:00 AM
        /// </summary>
        public void QuickSetSunrise()
        {
            SetTimeOfDay(6, 0);
            Debug.Log("Time set to Sunrise (6:00 AM)");
        }
        
        /// <summary>
        /// Morning - 9:00 AM
        /// </summary>
        public void QuickSetMorning()
        {
            SetTimeOfDay(9, 0);
            Debug.Log("Time set to Morning (9:00 AM)");
        }
        
        /// <summary>
        /// Noon/Midday - 12:00 PM
        /// </summary>
        public void QuickSetNoon()
        {
            SetTimeOfDay(12, 0);
            Debug.Log("Time set to Noon (12:00 PM)");
        }
        
        /// <summary>
        /// Afternoon - 15:00 (3:00 PM)
        /// </summary>
        public void QuickSetAfternoon()
        {
            SetTimeOfDay(15, 0);
            Debug.Log("Time set to Afternoon (3:00 PM)");
        }
        
        /// <summary>
        /// Sunset/Dusk - 18:00 (6:00 PM)
        /// </summary>
        public void QuickSetSunset()
        {
            SetTimeOfDay(18, 0);
            Debug.Log("Time set to Sunset (6:00 PM)");
        }
        
        /// <summary>
        /// Evening - 20:00 (8:00 PM)
        /// </summary>
        public void QuickSetEvening()
        {
            SetTimeOfDay(20, 0);
            Debug.Log("Time set to Evening (8:00 PM)");
        }
        
        /// <summary>
        /// Midnight - 0:00 (12:00 AM)
        /// </summary>
        public void QuickSetMidnight()
        {
            SetTimeOfDay(0, 0);
            Debug.Log("Time set to Midnight (12:00 AM)");
        }
        
        /// <summary>
        /// Late Night - 3:00 AM
        /// </summary>
        public void QuickSetLateNight()
        {
            SetTimeOfDay(3, 0);
            Debug.Log("Time set to Late Night (3:00 AM)");
        }

        private static void ApplyWeatherPreset(TenkokuModule ten, int idx)
        {
            if (ten == null) return;
            idx = Mathf.Clamp(idx, 0, 4);
            switch (idx)
            {
                case 0: // Clear
                    ten.weather_OvercastAmt = 0.0f;
                    ten.weather_RainAmt = 0.0f;
                    ten.weather_SnowAmt = 0.0f;
                    ten.weather_cloudCumulusAmt = 0.2f;
                    ten.weather_cloudCirrusAmt = 0.1f;
                    ten.weather_cloudAltoStratusAmt = 0.0f;
                    ten.weather_WindAmt = 0.1f;
                    ten.weather_lightning = 0.0f;
                    ten.weather_humidity = 0.2f;
                    break;
                case 1: // Cloudy
                    ten.weather_OvercastAmt = 0.6f;
                    ten.weather_RainAmt = 0.0f;
                    ten.weather_SnowAmt = 0.0f;
                    ten.weather_cloudCumulusAmt = 0.6f;
                    ten.weather_cloudCirrusAmt = 0.4f;
                    ten.weather_cloudAltoStratusAmt = 0.3f;
                    ten.weather_WindAmt = 0.25f;
                    ten.weather_lightning = 0.0f;
                    ten.weather_humidity = 0.4f;
                    break;
                case 2: // Rain
                    ten.weather_OvercastAmt = 0.85f;
                    ten.weather_RainAmt = 0.8f;
                    ten.weather_SnowAmt = 0.0f;
                    ten.weather_cloudCumulusAmt = 0.7f;
                    ten.weather_cloudCirrusAmt = 0.5f;
                    ten.weather_cloudAltoStratusAmt = 0.5f;
                    ten.weather_WindAmt = 0.35f;
                    ten.weather_lightning = 0.0f;
                    ten.weather_humidity = 0.9f;
                    break;
                case 3: // Storm
                    ten.weather_OvercastAmt = 0.95f;
                    ten.weather_RainAmt = 1.0f;
                    ten.weather_SnowAmt = 0.0f;
                    ten.weather_cloudCumulusAmt = 0.9f;
                    ten.weather_cloudCirrusAmt = 0.6f;
                    ten.weather_cloudAltoStratusAmt = 0.7f;
                    ten.weather_WindAmt = 0.7f;
                    ten.weather_lightning = 1.0f;
                    ten.weather_humidity = 1.0f;
                    break;
                case 4: // Snow
                    ten.weather_OvercastAmt = 0.85f;
                    ten.weather_RainAmt = 0.0f;
                    ten.weather_SnowAmt = 1.0f;
                    ten.weather_cloudCumulusAmt = 0.7f;
                    ten.weather_cloudCirrusAmt = 0.5f;
                    ten.weather_cloudAltoStratusAmt = 0.5f;
                    ten.weather_WindAmt = 0.3f;
                    ten.weather_lightning = 0.0f;
                    ten.weather_humidity = 0.8f;
                    break;
            }
        }

        private static void SetTimeOfDay(TenkokuModule ten, float time)
        {
            time = Mathf.Repeat(time, 24f);
            int h = Mathf.FloorToInt(time);
            int m = Mathf.FloorToInt((time - h) * 60f);
            ten.currentHour = h;
            ten.currentMinute = m;
        }
    }
}