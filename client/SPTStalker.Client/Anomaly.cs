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

        public void Tick(Vector3 playerPos, object health, float dt)
        {
            Animate(dt);

            var flat = new Vector2(playerPos.x - Position.x, playerPos.z - Position.z).magnitude;
            var inside = flat < Radius && Mathf.Abs(playerPos.y - Position.y) < VerticalTolerance;

            if (inside && !_inside)
            {
                OnEnter(playerPos, health);
            }

            _inside = inside;
            if (!inside)
            {
                _dotPending = 0;
                return;
            }

            // Burst effects (springboard slam, electro discharge...)
            if (_effect.burstDamage > 0)
            {
                _burstTimer -= dt;
                if (_burstTimer <= 0)
                {
                    Burst(playerPos, health);
                }
            }

            // Damage over time (burner, acid, radiation...)
            if (_effect.damagePerSecond > 0)
            {
                _dotPending += _effect.damagePerSecond * Plugin.DamageMultiplier.Value * dt;
                _dotTimer -= dt;
                if (_dotTimer <= 0)
                {
                    _dotTimer = DamageTick;
                    EftBridge.ApplyDamage(health, RandomPart(), _dotPending, _effect.damageType, playerPos, Vector3.up);
                    _dotPending = 0;
                }
            }
        }

        private void OnEnter(Vector3 playerPos, object health)
        {
            _dotTimer = DamageTick;
            if (_effect.burstDamage > 0)
            {
                Burst(playerPos, health);
            }

            if (Chance(_effect.intoxicationChancePercent))
            {
                EftBridge.DoIntoxication(health);
            }
        }

        private void Burst(Vector3 playerPos, object health)
        {
            _burstTimer = Mathf.Max(0.5f, _effect.burstInterval);
            _flash = 1f;

            var part = RandomPart();
            var dir = (playerPos - Position).normalized;
            EftBridge.ApplyDamage(health, part, _effect.burstDamage * Plugin.DamageMultiplier.Value, _effect.damageType, playerPos, dir);

            if (_effect.contusionSeconds > 0)
            {
                EftBridge.DoContusion(health, _effect.contusionSeconds, 0.6f);
            }

            if (_effect.stunSeconds > 0)
            {
                EftBridge.DoStun(health, _effect.stunSeconds, 0.5f);
            }

            if (Chance(_effect.fractureChancePercent))
            {
                EftBridge.DoFracture(health, part);
            }

            if (Chance(_effect.bleedChancePercent))
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
