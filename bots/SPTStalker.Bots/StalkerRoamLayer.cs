using System.Text;
using DrakiaXYZ.BigBrain.Brains;
using EFT;
using UnityEngine;

namespace SPTStalker.Bots
{
    /// <summary>
    /// Active while the stalker has nothing to fight: no current enemy, not under fire, not healing, and calm
    /// for a while. Everything else (combat, healing, reacting) stays with the game's own layers.
    /// </summary>
    internal class StalkerRoamLayer : CustomLayer
    {
        private float _lastDanger = -999f;
        private float _nextSainCheck;
        private bool _sainCalm = true;

        public StalkerRoamLayer(BotOwner botOwner, int priority) : base(botOwner, priority)
        {
        }

        public override string GetName() => "SPTStalkerRoam";

        public override bool IsActive()
        {
            if (!Plugin.RoamEnabled.Value || BotOwner == null || BotOwner.IsDead)
            {
                return false;
            }

            var memory = BotOwner.Memory;
            var danger = memory == null || memory.GoalEnemy != null || memory.IsUnderFire || (BotOwner.Medecine != null && BotOwner.Medecine.Using);
            if (danger)
            {
                _lastDanger = Time.time;
                return false;
            }

            if (Time.time - _lastDanger <= Plugin.CalmDownSeconds.Value)
            {
                return false;
            }

            // With SAIN in charge of this stalker, also ask SAIN: it tracks heard/remembered enemies and searches
            if (SainInterop.Installed && Time.time >= _nextSainCheck)
            {
                _nextSainCheck = Time.time + 0.5f;
                _sainCalm = !SainInterop.Controls(BotOwner) || SainInterop.IsCalm(BotOwner, Plugin.CalmDownSeconds.Value);
            }

            return !SainInterop.Installed || _sainCalm;
        }

        public override Action GetNextAction() => new Action(typeof(StalkerRoamLogic), "Roaming");

        public override bool IsCurrentActionEnding() => false;

        public override void BuildDebugText(StringBuilder stringBuilder)
        {
            stringBuilder.AppendLine($"SPT-Stalker roam, calm for {Time.time - _lastDanger:0}s" +
                                     (SainInterop.Installed ? $", SAIN {(SainInterop.Controls(BotOwner) ? SainInterop.Personality(BotOwner) : "not controlling")}" : ""));
        }
    }
}
