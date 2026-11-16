using System.Collections.Generic;
using DrakiaXYZ.BigBrain.Brains;
using EFT;
using UnityEngine;

namespace SPTStalker.Bots
{
    /// <summary>
    /// Logs each stalker once when it appears: name, brain and current AI layer. If the brain name isn't one the
    /// roam layer is registered for, the log says so, which makes first-time testing easy to read.
    /// </summary>
    internal class StalkerDiagnostics : MonoBehaviour
    {
        private readonly HashSet<int> _seen = new HashSet<int>();
        private float _next;

        private void Update()
        {
            if (Time.time < _next)
            {
                return;
            }

            _next = Time.time + 10f;
            var bots = FindObjectsOfType<BotOwner>();
            if (bots.Length == 0)
            {
                _seen.Clear();
                return;
            }

            foreach (var bot in bots)
            {
                if (bot == null || bot.Profile?.Info?.Settings == null || bot.Profile.Info.Settings.Role != Plugin.StalkerRole)
                {
                    continue;
                }

                if (!_seen.Add(bot.GetInstanceID()))
                {
                    continue;
                }

                var brain = bot.Brain?.BaseBrain?.ShortName() ?? "?";
                var leader = bot.Boss != null && bot.Boss.IamBoss ? "leader" : "follower";
                Plugin.Log.LogInfo($"SPT-Stalker Bots: stalker {bot.Profile.Nickname} ({leader}), brain '{brain}', active layer '{BrainManager.GetActiveLayerName(bot)}'");
            }
        }
    }
}
