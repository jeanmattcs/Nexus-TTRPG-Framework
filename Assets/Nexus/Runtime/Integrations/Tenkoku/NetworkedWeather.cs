using UnityEngine;
using Mirror;
using Tenkoku.Core;

namespace Nexus.Networking
{
    /// <summary>
    /// Synchronizes weather and time of day across all clients
    /// Only the host can change weather/time
    /// </summary>
    public class NetworkedWeather : NetworkBehaviour
    {
        [Header("Sync Settings")]
        [SyncVar(hook = nameof(OnTimeChanged))]
        private float syncTimeOfDay;
        
        [SyncVar(hook = nameof(OnWeatherChanged))]
        private int syncWeatherIndex;
        
        [SyncVar(hook = nameof(OnTimePausedChanged))]
        private bool syncTimePaused;

        private float updateInterval = 0.5f;
        private float lastUpdateTime;

        private void Start()
        {
            if (NetworkServer.active)
            {
                var ten = FindObjectOfType<TenkokuModule>();
                if (ten != null)
                {
                    syncTimeOfDay = GetTimeOfDay(ten);
                    syncWeatherIndex = GetCurrentWeatherIndex(ten);
                    syncTimePaused = IsPaused(ten);
                }
            }
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            var ten = FindObjectOfType<TenkokuModule>();
            if (ten != null)
            {
                SetTimeOfDay(ten, syncTimeOfDay);
                ApplyWeatherPreset(ten, syncWeatherIndex);
                if (syncTimePaused)
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

        private void Update()
        {
            if (!NetworkServer.active)
                return;

            var ten = FindObjectOfType<TenkokuModule>();
            if (ten == null) return;

            if (Time.time - lastUpdateTime > updateInterval)
            {
                float currentTime = GetTimeOfDay(ten);
                if (Mathf.Abs(currentTime - syncTimeOfDay) > 0.01f)
                {
                    syncTimeOfDay = currentTime;
                }

                int currentWeather = GetCurrentWeatherIndex(ten);
                if (currentWeather != syncWeatherIndex)
                {
                    syncWeatherIndex = currentWeather;
                }

                bool isPaused = IsPaused(ten);
                if (isPaused != syncTimePaused)
                {
                    syncTimePaused = isPaused;
                }

                lastUpdateTime = Time.time;
            }
        }

        // Hook methods
        private void OnTimeChanged(float oldTime, float newTime)
        {
            if (!NetworkServer.active)
            {
                var ten = FindObjectOfType<TenkokuModule>();
                if (ten != null)
                {
                    SetTimeOfDay(ten, newTime);
                }
            }
        }

        private void OnWeatherChanged(int oldIndex, int newIndex)
        {
            if (!NetworkServer.active)
            {
                var ten = FindObjectOfType<TenkokuModule>();
                if (ten != null)
                {
                    ApplyWeatherPreset(ten, newIndex);
                }
            }
        }

        private void OnTimePausedChanged(bool oldPaused, bool newPaused)
        {
            if (!NetworkServer.active)
            {
                var ten = FindObjectOfType<TenkokuModule>();
                if (ten != null)
                {
                    if (newPaused)
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

        // Commands for clients to request changes (only host should call these)
        [Command(requiresAuthority = false)]
        public void CmdSetTimeOfDay(float time)
        {
            var ten = FindObjectOfType<TenkokuModule>();
            if (ten != null)
            {
                SetTimeOfDay(ten, time);
                syncTimeOfDay = time;
            }
        }

        [Command(requiresAuthority = false)]
        public void CmdChangeWeather(int weatherIndex)
        {
            var ten = FindObjectOfType<TenkokuModule>();
            if (ten != null)
            {
                ApplyWeatherPreset(ten, weatherIndex);
                syncWeatherIndex = weatherIndex;
            }
        }

        [Command(requiresAuthority = false)]
        public void CmdSetTimePaused(bool paused)
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
                syncTimePaused = paused;
            }
        }

        private int GetCurrentWeatherIndex(TenkokuModule ten)
        {
            if (ten == null) return 0;

            if (ten.weather_SnowAmt > 0.5f) return 4; // Snow
            if (ten.weather_lightning > 0.25f || (ten.weather_RainAmt > 0.7f && ten.weather_OvercastAmt > 0.8f)) return 3; // Storm
            if (ten.weather_RainAmt > 0.2f) return 2; // Rain
            if (ten.weather_OvercastAmt > 0.4f || ten.weather_cloudCumulusAmt > 0.5f) return 1; // Cloudy
            return 0; // Clear
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

        private static float GetTimeOfDay(TenkokuModule ten)
        {
            return ten.currentHour + (ten.currentMinute / 60f);
        }

        private static void SetTimeOfDay(TenkokuModule ten, float time)
        {
            time = Mathf.Repeat(time, 24f);
            int h = Mathf.FloorToInt(time);
            int m = Mathf.FloorToInt((time - h) * 60f);
            ten.currentHour = h;
            ten.currentMinute = m;
        }

        private static bool IsPaused(TenkokuModule ten)
        {
            return (!ten.autoTime) || ten.timeCompression <= 0.0001f;
        }
    }
}
