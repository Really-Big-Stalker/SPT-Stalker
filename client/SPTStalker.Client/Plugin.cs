using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace SPTStalker.Client
{
    /// <summary>SPT-Stalker client plugin: builds the server-rolled anomaly fields inside the raid.</summary>
    [BepInPlugin("com.sptstalker.client", "SPT-Stalker", "0.2.0")]
    [BepInDependency("com.SPT.core")]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;

        // 0. Status
        internal static ConfigEntry<string> Status;

        // 1. Anomalies
        internal static ConfigEntry<bool> AnomaliesEnabled;
        internal static ConfigEntry<float> DamageMultiplier;

        // 2. Radiation
        internal static ConfigEntry<bool> RadiationEnabled;
        internal static ConfigEntry<float> RadiationIntakeMultiplier;
        internal static ConfigEntry<float> ArtifactRadiationMultiplier;
        internal static ConfigEntry<float> RadiationDamageMultiplier;
        internal static ConfigEntry<float> DoseDecayMultiplier;
        internal static ConfigEntry<float> CureMultiplier;

        // 3. Protection
        internal static ConfigEntry<float> GasMaskEffectiveness;
        internal static ConfigEntry<float> ArmorEffectiveness;

        // 4. Sounds
        internal static ConfigEntry<bool> DetectorEnabled;
        internal static ConfigEntry<float> DetectorRange;
        internal static ConfigEntry<float> DetectorVolume;
        internal static ConfigEntry<float> GeigerVolume;
        internal static ConfigEntry<float> HumVolume;

        // 5. Raid generation (sent to the server, applies from the next raid)
        internal static ConfigEntry<bool> SpawnAnomalies;
        internal static ConfigEntry<float> FieldCountMultiplier;
        internal static ConfigEntry<float> FieldSizeMultiplier;
        internal static ConfigEntry<float> ArtifactChanceMultiplier;

        // 6. Debug
        internal static ConfigEntry<bool> DebugMarkers;

        private void Awake()
        {
            Log = Logger;

            Status = Config.Bind("0. Status", "Status", "", new ConfigDescription(
                "Live readout: radiation dose, gas mask and armour.", null,
                new ConfigurationManagerAttributes { Order = 100, HideDefaultButton = true, HideSettingName = true, CustomDrawer = DrawStatus }));

            const string anomalies = "1. Anomalies";
            AnomaliesEnabled = Bind(anomalies, "Enabled", true, "Build anomaly fields in raid and apply their effects (needs the SPT-Stalker server mod).", 100);
            DamageMultiplier = Bind(anomalies, "Anomaly damage", 1f, "Scales all damage from anomalies.", 90, 0f, 5f);

            const string radiation = "2. Radiation";
            RadiationEnabled = Bind(radiation, "Enabled", true, "Track a radiation dose (shown in the health screen's radiation meter).", 100);
            RadiationIntakeMultiplier = Bind(radiation, "Radiation from anomalies", 1f, "Scales radiation picked up from anomalies and hotspots.", 90, 0f, 5f);
            ArtifactRadiationMultiplier = Bind(radiation, "Radiation from carried artifacts", 1f, "Scales radiation given off by artifacts you carry.", 80, 0f, 5f);
            RadiationDamageMultiplier = Bind(radiation, "Radiation sickness damage", 1f, "Scales damage caused by a high dose.", 70, 0f, 5f);
            DoseDecayMultiplier = Bind(radiation, "Natural recovery speed", 1f, "Scales how fast the body clears radiation by itself.", 60, 0f, 10f);
            CureMultiplier = Bind(radiation, "Alcohol strength", 1f, "Scales how much vodka, moonshine, whiskey and beer lower the dose.", 50, 0f, 5f);

            const string protection = "3. Protection";
            GasMaskEffectiveness = Bind(protection, "Gas mask effectiveness", 1f, "Scales gas mask protection (GP-7 90%, GP-5 80%...). Capped at full protection.", 100, 0f, 2f);
            ArmorEffectiveness = Bind(protection, "Armour effectiveness", 1f, "Scales how much armour softens physical anomaly hits.", 90, 0f, 2f);

            const string sounds = "4. Detector & sounds";
            DetectorEnabled = Bind(sounds, "Anomaly detector", true, "Beep faster as you get closer to an anomaly.", 100);
            DetectorRange = Bind(sounds, "Detector range", 20f, "Metres from an anomaly's edge at which the detector starts beeping.", 90, 5f, 60f);
            DetectorVolume = Bind(sounds, "Detector volume", 0.5f, "Anomaly detector beep volume.", 80, 0f, 1f);
            GeigerVolume = Bind(sounds, "Geiger counter volume", 0.5f, "Volume of the Geiger counter clicks.", 70, 0f, 1f);
            HumVolume = Bind(sounds, "Anomaly hum volume", 0.6f, "Volume of the anomalies' own sound (applies to newly built anomalies).", 60, 0f, 1f);

            const string generation = "5. Raid generation (next raid)";
            SpawnAnomalies = Bind(generation, "Spawn anomalies", true, "Have the server roll anomaly fields for your raids.", 100);
            FieldCountMultiplier = Bind(generation, "Number of fields", 1f, "Scales how many anomaly fields each map gets.", 90, 0f, 5f);
            FieldSizeMultiplier = Bind(generation, "Anomalies per field", 1f, "Scales how many anomalies make up each field.", 80, 0.2f, 5f);
            ArtifactChanceMultiplier = Bind(generation, "Artifact chance in fields", 1f, "Scales the chance a field holds an artifact.", 70, 0f, 10f);

            DebugMarkers = Bind("6. Debug", "Show anomaly markers", false, "Draw a visible sphere for every anomaly (applies to newly built anomalies).", 100);

            SettingsSync.Watch(SpawnAnomalies, FieldCountMultiplier, FieldSizeMultiplier, ArtifactChanceMultiplier);
            RadiationPatches.Apply(new Harmony("com.sptstalker.client"));

            var host = new GameObject("SPTStalker.AnomalyManager");
            DontDestroyOnLoad(host);
            host.AddComponent<AnomalyManager>();

            Log.LogInfo("SPT-Stalker client loaded (settings: F12)");
        }

        private ConfigEntry<T> Bind<T>(string section, string key, T value, string description, int order)
        {
            return Config.Bind(section, key, value, new ConfigDescription(description, null,
                new ConfigurationManagerAttributes { Order = order }));
        }

        private ConfigEntry<float> Bind(string section, string key, float value, string description, int order, float min, float max)
        {
            return Config.Bind(section, key, value, new ConfigDescription(description, new AcceptableValueRange<float>(min, max),
                new ConfigurationManagerAttributes { Order = order }));
        }

        private static void DrawStatus(ConfigEntryBase entry)
        {
            GUILayout.Label(StatusInfo.Text);
        }
    }

    /// <summary>Text for the F12 status line, updated while in raid.</summary>
    internal static class StatusInfo
    {
        public static string Text = "Not in raid";
    }
}
