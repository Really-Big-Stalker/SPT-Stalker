using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SPTStalker.Client
{
    /// <summary>
    /// Works out how much the player's worn armour softens physical anomaly hits.
    /// Body: the highest armour class among the vest/rig slots (plates and soft inserts included) protects every
    /// body part except the head, like a stalker suit. Head: the highest class in the headwear slot.
    /// </summary>
    internal class ArmorTracker
    {
        private readonly ArmorSettings _settings;
        private readonly Dictionary<string, int> _classes;
        private float _scanTimer;
        private int _bodyClass, _headClass;

        public float BodyReduction { get; private set; }
        public float HeadReduction { get; private set; }

        public ArmorTracker(ArmorSettings settings, ArmorClass[] classes)
        {
            _settings = settings;
            _classes = (classes ?? new ArmorClass[0]).Where(c => c.tpl != null)
                .GroupBy(c => c.tpl).ToDictionary(g => g.Key, g => g.Max(c => c.armorClass));
        }

        public void Tick(object player, float dt)
        {
            _scanTimer -= dt;
            if (_scanTimer > 0)
            {
                return;
            }

            _scanTimer = 2f;
            var body = BestClass(EftBridge.SlotTemplateIds(player, _settings.bodySlots));
            var head = BestClass(EftBridge.SlotTemplateIds(player, _settings.headSlots));
            if (body != _bodyClass || head != _headClass)
            {
                Plugin.Log.LogInfo($"SPT-Stalker: anomaly armour - body class {body}, head class {head}");
            }

            _bodyClass = body;
            _headClass = head;
            BodyReduction = Reduction(body);
            HeadReduction = Reduction(head);
        }

        /// <summary>Share of a physical hit on `bodyPart` that armour absorbs (before the anomaly's own armorProtection).</summary>
        public float For(string bodyPart) => bodyPart == "Head" ? HeadReduction : BodyReduction;

        private int BestClass(List<string> tpls)
        {
            var best = 0;
            foreach (var tpl in tpls)
            {
                if (tpl != null && _classes.TryGetValue(tpl, out var c) && c > best)
                {
                    best = c;
                }
            }

            return best;
        }

        private float Reduction(int armorClass) => Mathf.Clamp(armorClass * _settings.reductionPerClass, 0f, _settings.maxReduction);
    }
}
