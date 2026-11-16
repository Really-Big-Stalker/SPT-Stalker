using UnityEngine;

namespace SPTStalker.Client
{
    /// <summary>
    /// One anomaly in the world: a light (and optional marker) that flickers, a looping hum, and the effect
    /// applied to the local player while they are inside its radius.
    /// </summary>
    internal class Anomaly
    {
        private const float VerticalTolerance = 2.5f;
        private const float DamageTick = 0.5f;

        public readonly Vector3 Position;
        public readonly float Radius;
        private readonly string _type;
        private readonly AnomalyEffect _effect;
        private readonly GameObject _go;
        private readonly Light _light;
        private readonly float _seed;

        private bool _inside;
        private float _burstTimer;
        private float _dotTimer;
        private float _dotPending;
        private float _flash;

        public Anomaly(Transform parent, string type, AnomalyEffect effect, Vector3 position, float radius, int seed)
        {
            _type = type;
            _effect = effect;
            Radius = radius;
            Position = SnapToGround(position);
            _seed = seed * 0.37f;

            _go = new GameObject($"SPTStalker.Anomaly.{type}");
            _go.transform.SetParent(parent, false);
            _go.transform.position = Position + Vector3.up * 0.4f;

            if (effect.lightIntensity > 0 && effect.lightColor != null && effect.lightColor.Length >= 3)
            {
                _light = _go.AddComponent<Light>();
                _light.type = LightType.Point;
                _light.color = new Color(effect.lightColor[0], effect.lightColor[1], effect.lightColor[2]);
                _light.range = radius * 2.5f;
                _light.intensity = effect.lightIntensity;
                _light.shadows = LightShadows.None;
            }

            if (effect.humPitch > 0 && Plugin.HumVolume.Value > 0)
            {
                var audio = _go.AddComponent<AudioSource>();
                audio.clip = ProceduralAudio.Hum(type, effect.humPitch, effect.flicker > 3 ? 1f : effect.flicker > 1.5f ? 0.4f : 0f, seed);
                audio.loop = true;
                audio.spatialBlend = 1f;
                audio.rolloffMode = AudioRolloffMode.Linear;
                audio.minDistance = 0.5f;
                audio.maxDistance = radius * 4f + 6f;
                audio.dopplerLevel = 0f;
                audio.volume = Plugin.HumVolume.Value;
                audio.time = (seed % 100) / 100f; // de-sync neighbours
                audio.Play();
            }

            if (Plugin.DebugMarkers.Value)
            {
                var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Object.Destroy(marker.GetComponent<Collider>());
                marker.transform.SetParent(_go.transform, false);
                marker.transform.position = Position;
                marker.transform.localScale = Vector3.one * radius * 2f;
            }
        }

        /// <summary>Distance from the anomaly's edge (0 when inside).</summary>
        public float EdgeDistance(Vector3 point) => Mathf.Max(0f, Vector3.Distance(point, Position) - Radius);

        /// <summary>
        /// Runs the anomaly for one frame. Returns the radiation around the player from this anomaly
        /// (ambient, for the Geiger counter) and how much of it gets through the gas mask (intake).
        /// </summary>
        /// <param name="maskFactor">0..1 protection of the gas mask the player is wearing.</param>
        public (float ambient, float intake) Tick(Vector3 playerPos, object health, float dt, float maskFactor, ArmorTracker armor)
        {
            Animate(dt);

            var flat = new Vector2(playerPos.x - Position.x, playerPos.z - Position.z).magnitude;
            var level = Mathf.Abs(playerPos.y - Position.y) < VerticalTolerance * 2f;
            var inside = flat < Radius && Mathf.Abs(playerPos.y - Position.y) < VerticalTolerance;

            // What a gas mask lets through of this anomaly's airborne harm
            var through = 1f - Mathf.Clamp01(maskFactor) * Mathf.Clamp01(_effect.maskProtection);

            // Radiation: full inside, fading out across the aura ring around it
            var ambient = 0f;
            if (inside)
            {
                ambient = _effect.radiationPerSecond;
            }
            else if (level && _effect.auraRadiationPerSecond > 0)
            {
                var aura = Radius * Mathf.Max(1f, _effect.auraRadiusMultiplier);
                if (flat < aura)
                {
                    ambient = _effect.auraRadiationPerSecond * (1f - (flat - Radius) / Mathf.Max(0.01f, aura - Radius));
                }
            }

            if (inside && !_inside)
            {
                OnEnter(playerPos, health, through, armor);
            }

            _inside = inside;
            if (!inside)
            {
                _dotPending = 0;
                return (ambient, ambient * through);
            }

            // Burst effects (springboard slam, electro discharge...): a mask doesn't help
            if (_effect.burstDamage > 0)
            {
                _burstTimer -= dt;
                if (_burstTimer <= 0)
                {
                    Burst(playerPos, health, armor);
                }
            }

            // Damage over time (burner, acid, radiation...): airborne part reduced by the mask
            if (_effect.damagePerSecond > 0)
            {
                _dotPending += _effect.damagePerSecond * through * Plugin.DamageMultiplier.Value * dt;
                _dotTimer -= dt;
                if (_dotTimer <= 0)
                {
                    _dotTimer = DamageTick;
                    EftBridge.ApplyDamage(health, RandomPart(), _dotPending, _effect.damageType, playerPos, Vector3.up);
                    _dotPending = 0;
                }
            }

            return (ambient, ambient * through);
        }

        private void OnEnter(Vector3 playerPos, object health, float through, ArmorTracker armor)
        {
            _dotTimer = DamageTick;
            if (_effect.burstDamage > 0)
            {
                Burst(playerPos, health, armor);
            }

            if (Chance(_effect.intoxicationChancePercent * through))
            {
                EftBridge.DoIntoxication(health);
            }
        }

        private void Burst(Vector3 playerPos, object health, ArmorTracker armor)
        {
            _burstTimer = Mathf.Max(0.5f, _effect.burstInterval);
            _flash = 1f;

            var part = RandomPart();
            // Armour absorbs part of the physical hit: damage, knock-out effects and injury chances
            var absorbed = armor == null ? 0f : armor.For(part) * Mathf.Clamp01(_effect.armorProtection);
            var left = 1f - absorbed;

            var dir = (playerPos - Position).normalized;
            EftBridge.ApplyDamage(health, part, _effect.burstDamage * left * Plugin.DamageMultiplier.Value, _effect.damageType, playerPos, dir);

            if (_effect.contusionSeconds * left > 0.3f)
            {
                EftBridge.DoContusion(health, _effect.contusionSeconds * left, 0.6f * left);
            }

            if (_effect.stunSeconds * left > 0.3f)
            {
                EftBridge.DoStun(health, _effect.stunSeconds * left, 0.5f * left);
            }

            if (Chance(_effect.fractureChancePercent * left))
            {
                EftBridge.DoFracture(health, part);
            }

            if (Chance(_effect.bleedChancePercent * left))
            {
                EftBridge.DoBleed(health, part);
            }
        }

        private void Animate(float dt)
        {
            if (_light == null)
            {
                return;
            }

            _flash = Mathf.Max(0f, _flash - dt * 6f);
            var noise = Mathf.PerlinNoise(Time.time * Mathf.Max(0.1f, _effect.flicker), _seed);
            _light.intensity = _effect.lightIntensity * (0.55f + 0.6f * noise) * (1f + 3f * _flash);
        }

        public void Destroy()
        {
            if (_go != null)
            {
                Object.Destroy(_go);
            }
        }

        private string RandomPart()
        {
            var parts = _effect.bodyParts;
            return parts == null || parts.Length == 0 ? "Chest" : parts[Random.Range(0, parts.Length)];
        }

        private static bool Chance(float percent) => percent > 0 && Random.value * 100f < percent;

        private static Vector3 SnapToGround(Vector3 p)
        {
            // Anomalies are rolled around loot points; drop each onto whatever surface is below it
            if (Physics.Raycast(p + Vector3.up * 2f, Vector3.down, out var hit, 8f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                return hit.point;
            }

            return p;
        }
    }
}
