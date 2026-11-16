using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using DrakiaXYZ.BigBrain.Brains;
using EFT;
using UnityEngine;

namespace SPTStalker.Bots
{
    /// <summary>
    /// Roaming Loner stalkers. The server mod spawns them (role arenaFighterEvent) with stalker gear and names;
    /// this plugin adds a BigBrain layer that makes groups wander between this raid's anomaly fields and other
    /// points, search around, and move on. Combat is left to the game's own AI.
    /// Waypoints (DrakiaXYZ) is recommended: its extra navmesh lets them reach far more of each map.
    /// </summary>
    [BepInPlugin("com.sptstalker.bots", "SPT-Stalker Bots", "0.1.0")]
    [BepInDependency("xyz.drakia.bigbrain", "1.5.0")]
    [BepInDependency("xyz.drakia.waypoints", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;

        internal static ConfigEntry<bool> RoamEnabled;
        internal static ConfigEntry<float> SearchSecondsMin;
        internal static ConfigEntry<float> SearchSecondsMax;
        internal static ConfigEntry<float> MoveSpeed;
        internal static ConfigEntry<float> CalmDownSeconds;
        internal static ConfigEntry<float> AnomalyPreference;
        internal static ConfigEntry<bool> DebugLog;

        /// <summary>Role the server mod gives the stalkers (data/bots/stalkers.json "role").</summary>
        internal const WildSpawnType StalkerRole = WildSpawnType.arenaFighterEvent;

        // BigBrain matches layers on the bot's brain name AND role; the role filter already limits this to
        // stalkers, so list every brain that role might get rather than depend on one internal name.
        private static readonly List<string> BrainNames = new List<string>
        {
            "ArenaFighter", "Assault", "PMC", "ExUsec", "PmcBear", "PmcUsec", "CursAssault", "Obdolbs",
            "SectantWarrior", "Marksman", "BossKilla", "Knight", "BigPipe", "BirdEye"
        };

        private void Awake()
        {
            Log = Logger;
            const string section = "Loner stalkers";
            RoamEnabled = Config.Bind(section, "Roaming", true, new ConfigDescription("Stalker groups wander between anomaly fields and points of interest.", null, new ConfigurationManagerAttributes { Order = 100 }));
            AnomalyPreference = Config.Bind(section, "Anomaly field preference", 0.7f, new ConfigDescription("Chance a group heads for an anomaly field (to hunt artifacts) instead of a random spot.", new AcceptableValueRange<float>(0f, 1f), new ConfigurationManagerAttributes { Order = 90 }));
            SearchSecondsMin = Config.Bind(section, "Search time min (s)", 25f, new ConfigDescription("How long a group searches when it arrives somewhere.", new AcceptableValueRange<float>(0f, 300f), new ConfigurationManagerAttributes { Order = 80 }));
            SearchSecondsMax = Config.Bind(section, "Search time max (s)", 60f, new ConfigDescription("How long a group searches when it arrives somewhere.", new AcceptableValueRange<float>(0f, 300f), new ConfigurationManagerAttributes { Order = 70 }));
            MoveSpeed = Config.Bind(section, "Walking speed", 0.6f, new ConfigDescription("0 = slow walk, 1 = full walk speed.", new AcceptableValueRange<float>(0.1f, 1f), new ConfigurationManagerAttributes { Order = 60 }));
            CalmDownSeconds = Config.Bind(section, "Calm down after fight (s)", 30f, new ConfigDescription("Seconds without enemies before they go back to roaming.", new AcceptableValueRange<float>(0f, 300f), new ConfigurationManagerAttributes { Order = 50 }));
            DebugLog = Config.Bind(section, "Debug log", false, new ConfigDescription("Log what each stalker group is doing.", null, new ConfigurationManagerAttributes { Order = 10, IsAdvanced = true }));

            BrainManager.AddCustomLayer(typeof(StalkerRoamLayer), BrainNames, 19, new List<WildSpawnType> { StalkerRole });

            var host = new GameObject("SPTStalker.Bots.Diagnostics");
            DontDestroyOnLoad(host);
            host.AddComponent<StalkerDiagnostics>();

            Log.LogInfo($"SPT-Stalker Bots loaded (Waypoints {(Chainloader().ContainsKey("xyz.drakia.waypoints") ? "found" : "not installed - recommended")})");
        }

        private static Dictionary<string, PluginInfo> Chainloader() => BepInEx.Bootstrap.Chainloader.PluginInfos;
    }

    internal sealed class ConfigurationManagerAttributes
    {
#pragma warning disable 0649
        public int? Order;
        public bool? IsAdvanced;
#pragma warning restore 0649
    }
}
