using System.Globalization;
using BepInEx.Configuration;

namespace SPTStalker.Client
{
    /// <summary>
    /// Sends the "Raid generation" settings to the server (POST /sptstalker/settings) whenever they change and once
    /// after start-up. The server uses them when it rolls the next raid's anomalies. Retries until the server
    /// accepts (e.g. before you've logged in there is no session yet).
    /// </summary>
    internal static class SettingsSync
    {
        private static bool _dirty = true;
        private static float _nextTry;

        public static void Watch(params ConfigEntryBase[] entries)
        {
            foreach (var entry in entries)
            {
                entry.ConfigFile.SettingChanged += (_, args) =>
                {
                    if (args.ChangedSetting == entry)
                    {
                        _dirty = true;
                    }
                };
            }
        }

        /// <summary>Called regularly by the manager; does nothing unless something changed.</summary>
        public static void TrySend()
        {
            if (!_dirty || UnityEngine.Time.unscaledTime < _nextTry || !EftBridge.Init())
            {
                return;
            }

            _nextTry = UnityEngine.Time.unscaledTime + 10f;

            var json = string.Format(CultureInfo.InvariantCulture,
                "{{\"anomaliesEnabled\":{0},\"fieldCountMultiplier\":{1},\"fieldSizeMultiplier\":{2},\"artifactChanceMultiplier\":{3}}}",
                Plugin.SpawnAnomalies.Value ? "true" : "false",
                Plugin.FieldCountMultiplier.Value, Plugin.FieldSizeMultiplier.Value, Plugin.ArtifactChanceMultiplier.Value);

            try
            {
                var response = EftBridge.PostJson("/sptstalker/settings", json);
                if (response != null && response.Contains("ok"))
                {
                    _dirty = false;
                    Plugin.Log.LogInfo("SPT-Stalker: raid settings sent to the server");
                }
            }
            catch
            {
                // Not logged in yet / server busy: try again later
            }
        }
    }
}
