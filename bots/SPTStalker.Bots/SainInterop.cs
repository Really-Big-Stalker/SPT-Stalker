using System;
using System.Linq;
using System.Reflection;
using EFT;
using UnityEngine;

namespace SPTStalker.Bots
{
    /// <summary>
    /// Optional SAIN support (me.sol.sain, SPT 4.x build by ArchangelWTF), through SAIN's public
    /// SAIN.Interop.SAINExternal API, called by reflection (the same way QuestingBots does), so this plugin works
    /// the same with or without SAIN installed.
    ///
    /// SAIN already drives the Bloodhound role (our stalkers) with its own combat layers (priority 20-24); the roam
    /// layer sits just below at 19. SAIN tracks enemies itself, so when it controls a stalker we ask SAIN whether
    /// the bot is calm instead of relying only on the vanilla memory.
    /// </summary>
    internal static class SainInterop
    {
        private static bool _initialized;
        private static MethodInfo _timeSinceSenseEnemy, _canBotQuest, _getPersonality;

        public static bool Installed { get; private set; }

        public static void Init()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            if (!BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("me.sol.sain"))
            {
                return;
            }

            var external = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("SAIN.Interop.SAINExternal", false))
                .FirstOrDefault(t => t != null);
            if (external == null)
            {
                Plugin.Log.LogWarning("SPT-Stalker Bots: SAIN is installed but SAIN.Interop.SAINExternal wasn't found; using vanilla checks");
                return;
            }

            _timeSinceSenseEnemy = external.GetMethod("TimeSinceSenseEnemy", new[] { typeof(BotOwner) });
            _canBotQuest = external.GetMethod("CanBotQuest", new[] { typeof(BotOwner), typeof(Vector3), typeof(float) });
            _getPersonality = external.GetMethod("GetPersonality", new[] { typeof(BotOwner) });
            Installed = _timeSinceSenseEnemy != null && _canBotQuest != null && _getPersonality != null;

            Plugin.Log.LogInfo(Installed
                ? "SPT-Stalker Bots: SAIN detected, stalkers use SAIN's enemy awareness"
                : "SPT-Stalker Bots: SAIN detected but its external API differs from what's expected; using vanilla checks");
        }

        /// <summary>SAIN personality of the bot, or empty when SAIN doesn't control it.</summary>
        public static string Personality(BotOwner bot)
        {
            if (!Installed)
            {
                return string.Empty;
            }

            try
            {
                return _getPersonality.Invoke(null, new object[] { bot }) as string ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        public static bool Controls(BotOwner bot) => Personality(bot).Length > 0;

        /// <summary>
        /// True when SAIN says the bot is free to wander: not fighting, not searching a last known enemy position,
        /// and no enemy sensed for `calmSeconds`. Only meaningful when <see cref="Controls"/> is true.
        /// </summary>
        public static bool IsCalm(BotOwner bot, float calmSeconds)
        {
            try
            {
                var sinceEnemy = (float)_timeSinceSenseEnemy.Invoke(null, new object[] { bot });
                if (sinceEnemy < calmSeconds)
                {
                    return false;
                }

                return (bool)_canBotQuest.Invoke(null, new object[] { bot, bot.Position, 0.33f });
            }
            catch
            {
                return true;
            }
        }
    }
}
