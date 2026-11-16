using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;

namespace SPTStalker.Client
{
    /// <summary>SPT-Stalker client plugin: builds the server-rolled anomaly fields inside the raid.</summary>
    [BepInPlugin("com.sptstalker.client", "SPT-Stalker Client", "0.1.0")]
    [BepInDependency("com.SPT.core")]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;

        internal static ConfigEntry<bool> AnomaliesEnabled;
        internal static ConfigEntry<float> DamageMultiplier;
        internal static ConfigEntry<bool> DetectorEnabled;
        internal static ConfigEntry<float> DetectorRange;
        internal static ConfigEntry<float> DetectorVolume;
        internal static ConfigEntry<float> HumVolume;
        internal static ConfigEntry<bool> DebugMarkers;

        private void Awake()
        {
            Log = Logger;

            AnomaliesEnabled = Config.Bind("Anomalies", "Enabled", true, "Build anomaly fields in raid (needs the SPT-Stalker server mod).");
            DamageMultiplier = Config.Bind("Anomalies", "Damage multiplier", 1f, new ConfigDescription("Scales all anomaly damage.", new AcceptableValueRange<float>(0f, 5f)));
            HumVolume = Config.Bind("Anomalies", "Hum volume", 0.6f, new ConfigDescription("Volume of the anomalies' own sound.", new AcceptableValueRange<float>(0f, 1f)));
            DetectorEnabled = Config.Bind("Detector", "Enabled", true, "Beep faster as you get closer to an anomaly.");
            DetectorRange = Config.Bind("Detector", "Range", 20f, new ConfigDescription("Metres from an anomaly's edge at which the detector starts beeping.", new AcceptableValueRange<float>(5f, 60f)));
            DetectorVolume = Config.Bind("Detector", "Volume", 0.5f, new ConfigDescription("Detector beep volume.", new AcceptableValueRange<float>(0f, 1f)));
            DebugMarkers = Config.Bind("Debug", "Show markers", false, "Draw a visible sphere for every anomaly (for testing layouts).");

            var host = new GameObject("SPTStalker.AnomalyManager");
            DontDestroyOnLoad(host);
            host.AddComponent<AnomalyManager>();

            Log.LogInfo("SPT-Stalker client loaded");
        }
    }
}
