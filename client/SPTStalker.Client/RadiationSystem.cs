using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SPTStalker.Client
{
    /// <summary>
    /// The local player's radiation dose for the current raid. Builds up from anomalies, radiation hotspots and
    /// carried artifacts; a gas mask cuts what you breathe in; vodka and moonshine bring it down; the body clears
    /// a little by itself. Higher doses cause radiation damage and sickness. The dose is shown in the health
    /// screen's (normally unused) radiation meter, and a Geiger counter clicks with the ambient level.
    /// </summary>
    internal class RadiationSystem
    {
        /// <summary>Read by the health-screen patch.</summary>
        public static float Dose { get; private set; }
        public static float MaxDose { get; private set; } = 100;
        public static float WarningDose { get; private set; } = 25;
        public static bool Active { get; private set; }

        private static RadiationSystem _current;

        private readonly RadiationSettings _settings;
        private readonly Dictionary<string, float> _artifactRadiation;
        private readonly Dictionary<string, float> _maskProtection;
        private readonly Dictionary<string, RadiationCure> _cures;
        private readonly AudioSource _geiger;
        private readonly AudioClip _click;

        private object _health;
        private float _maskFactor;
        private string _maskName;
        private float _carried;
        private float _scanTimer;
        private float _damageTimer;
        private float _sicknessTimer;
        private int _stageIndex = -1;

        public string StageName => _stageIndex >= 0 ? _settings.stages[_stageIndex].name : "clean";

        public float MaskFactor => Mathf.Clamp01(_maskFactor * Plugin.GasMaskEffectiveness.Value);
        public string MaskName => _maskName;

        public RadiationSystem(RadiationSettings settings, ArtifactRadiation[] artifacts, GameObject host)
        {
            _settings = settings;
            _artifactRadiation = (artifacts ?? new ArtifactRadiation[0]).Where(a => a.tpl != null)
                .GroupBy(a => a.tpl).ToDictionary(g => g.Key, g => g.First().radiationPerSecond);
            _maskProtection = (settings.gasMasks ?? new GasMask[0]).Where(m => m.tpl != null)
                .GroupBy(m => m.tpl).ToDictionary(g => g.Key, g => Mathf.Clamp01(g.First().protection));
            _cures = (settings.cures ?? new RadiationCure[0]).Where(c => c.tpl != null)
                .GroupBy(c => c.tpl).ToDictionary(g => g.Key, g => g.First());

            Dose = 0;
            MaxDose = settings.maxDose > 0 ? settings.maxDose : 100;
            WarningDose = settings.stages != null && settings.stages.Length > 0 ? settings.stages.Min(s => s.dose) : MaxDose * 0.25f;
            Active = true;
            _current = this;

            _geiger = host.AddComponent<AudioSource>();
            _geiger.spatialBlend = 0f;
            _geiger.playOnAwake = false;
            _click = ProceduralAudio.GeigerClick();
        }

        /// <param name="ambient">Radiation around the player this frame (what a Geiger counter measures).</param>
        /// <param name="intake">Radiation actually absorbed this frame from anomalies (after the gas mask).</param>
        public void Tick(object player, object health, float ambient, float intake, float dt)
        {
            _health = health;
            TickScan(player, dt);

            // Carried artifacts irradiate you directly; a mask doesn't help
            var carried = _carried * Plugin.ArtifactRadiationMultiplier.Value;
            var total = intake * Plugin.RadiationIntakeMultiplier.Value + carried;
            Dose = Mathf.Clamp(Dose + (total - _settings.decayPerSecond * Plugin.DoseDecayMultiplier.Value) * dt, 0, MaxDose);

            Geiger(ambient * Plugin.RadiationIntakeMultiplier.Value + carried, dt);
            ApplyStage(player, dt);
        }

        /// <summary>Every 2 s: what's on your face, and which artifacts you're carrying (also runs with radiation off, for gas masks).</summary>
        public void TickScan(object player, float dt)
        {
            _scanTimer -= dt;
            if (_scanTimer <= 0)
            {
                _scanTimer = 2f;
                Scan(player);
            }
        }

        private void Scan(object player)
        {
            var mask = EftBridge.FaceCoverTemplateId(player);
            var factor = mask != null && _maskProtection.TryGetValue(mask, out var p) ? p : 0f;
            if (!Mathf.Approximately(factor, _maskFactor))
            {
                _maskName = factor > 0 ? (_settings.gasMasks.FirstOrDefault(m => m.tpl == mask)?.name ?? mask) : null;
                Plugin.Log.LogInfo(factor > 0 ? $"SPT-Stalker: wearing {_maskName} ({factor:P0} protection)" : "SPT-Stalker: no gas mask");
            }

            _maskFactor = factor;
            _carried = 0;
            if (_artifactRadiation.Count > 0)
            {
                foreach (var tpl in EftBridge.CarriedTemplateIds(player, _settings.shieldedSlots, _settings.shieldedSubSlots))
                {
                    if (tpl != null && _artifactRadiation.TryGetValue(tpl, out var rate))
                    {
                        _carried += rate;
                    }
                }
            }
        }

        private void ApplyStage(object player, float dt)
        {
            var stages = _settings.stages;
            if (stages == null || stages.Length == 0)
            {
                return;
            }

            var index = -1;
            for (var i = 0; i < stages.Length; i++)
            {
                if (Dose >= stages[i].dose && (index < 0 || stages[i].dose >= stages[index].dose))
                {
                    index = i;
                }
            }

            if (index != _stageIndex)
            {
                if (index >= 0 && (_stageIndex < 0 || stages[index].dose > stages[_stageIndex].dose))
                {
                    Plugin.Log.LogInfo($"SPT-Stalker: radiation {Dose:0}/{MaxDose:0} - {stages[index].name}");
                    if (stages[index].gameRadiationEffect)
                    {
                        EftBridge.DoRadExposure(_health);
                    }
                }

                _stageIndex = index;
                _sicknessTimer = index >= 0 ? stages[index].sicknessEverySeconds : 0;
            }

            if (index < 0)
            {
                return;
            }

            var stage = stages[index];
            _damageTimer -= dt;
            if (_damageTimer <= 0 && stage.damagePerSecond > 0)
            {
                _damageTimer = 1f;
                var parts = stage.bodyParts == null || stage.bodyParts.Length == 0 ? new[] { "Chest" } : stage.bodyParts;
                EftBridge.ApplyDamage(_health, parts[Random.Range(0, parts.Length)], stage.damagePerSecond * Plugin.RadiationDamageMultiplier.Value,
                    "RadExposure", EftBridge.PlayerPosition(player), Vector3.up);
            }

            if (stage.sicknessEverySeconds > 0)
            {
                _sicknessTimer -= dt;
                if (_sicknessTimer <= 0)
                {
                    _sicknessTimer = stage.sicknessEverySeconds * Random.Range(0.7f, 1.3f);
                    EftBridge.DoContusion(_health, 4f, 0.35f);
                }
            }
        }

        private void Geiger(float level, float dt)
        {
            if (Plugin.GeigerVolume.Value <= 0 || level <= 0.005f)
            {
                return;
            }

            // Random clicks, rate rising with the level (capped so it doesn't turn into a tone)
            var clicksPerSecond = Mathf.Min(45f, level * 18f);
            if (Random.value < clicksPerSecond * dt)
            {
                _geiger.pitch = Random.Range(0.9f, 1.15f);
                _geiger.PlayOneShot(_click, Plugin.GeigerVolume.Value);
            }
        }

        /// <summary>Called by the item-use patch when the local player drinks/uses something.</summary>
        public static void OnItemUsed(object health, object item)
        {
            var current = _current;
            if (current == null || !Active || health == null || !ReferenceEquals(health, current._health))
            {
                return;
            }

            var tpl = EftBridge.TemplateId(item);
            if (tpl != null && current._cures.TryGetValue(tpl, out var cure))
            {
                Dose = Mathf.Max(0, Dose - cure.doseReduction * Plugin.CureMultiplier.Value);
                Plugin.Log.LogInfo($"SPT-Stalker: {cure.name} -{cure.doseReduction:0} radiation (now {Dose:0}/{MaxDose:0})");
            }
        }

        public void Dispose()
        {
            Active = false;
            Dose = 0;
            if (_current == this)
            {
                _current = null;
            }
        }
    }
}
