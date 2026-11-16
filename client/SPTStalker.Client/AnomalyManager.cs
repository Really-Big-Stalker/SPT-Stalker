using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SPTStalker.Client
{
    /// <summary>
    /// Watches for a raid starting, fetches that raid's anomaly layout from the server, builds the anomalies,
    /// runs their effects against the local player, drives the detector, and cleans up when the raid ends.
    /// </summary>
    internal class AnomalyManager : MonoBehaviour
    {
        private const float PollInterval = 1f;

        private readonly List<Anomaly> _anomalies = new List<Anomaly>();
        private MonoBehaviour _world;
        private GameObject _root;
        private bool _built;
        private float _poll;
        private bool _bridgeFailed;

        private AudioSource _detector;
        private RadiationSystem _radiation;
        private float _beepTimer;

        private void Update()
        {
            if (!Plugin.AnomaliesEnabled.Value || _bridgeFailed)
            {
                return;
            }

            // Raid ended (GameWorld destroyed) -> clean up
            if (_built && _world == null)
            {
                Cleanup();
            }

            if (!_built)
            {
                _poll -= Time.unscaledDeltaTime;
                if (_poll > 0)
                {
                    return;
                }

                _poll = PollInterval;
                TryBuild();
                return;
            }

            var player = EftBridge.MainPlayer(_world);
            if (player == null)
            {
                return;
            }

            var health = EftBridge.Health(player);
            if (!EftBridge.IsAlive(health))
            {
                return;
            }

            var pos = EftBridge.PlayerPosition(player);
            var dt = Time.deltaTime;
            var nearest = float.MaxValue;
            var mask = _radiation?.MaskFactor ?? 0f;
            float ambient = 0f, intake = 0f;
            foreach (var anomaly in _anomalies)
            {
                var (a, i) = anomaly.Tick(pos, health, dt, mask);
                ambient += a;
                intake += i;
                nearest = Mathf.Min(nearest, anomaly.EdgeDistance(pos));
            }

            UpdateDetector(nearest, dt);

            if (_radiation != null && Plugin.RadiationEnabled.Value)
            {
                _radiation.Tick(player, health, ambient, intake, dt);
            }
        }

        private void TryBuild()
        {
            if (!EftBridge.Init())
            {
                _bridgeFailed = true;
                return;
            }

            var world = EftBridge.FindGameWorld();
            if (world == null || EftBridge.MainPlayer(world) == null)
            {
                return;
            }

            _world = world;
            _built = true; // one attempt per raid, even if the server has nothing for us

            AnomalyLayout layout;
            try
            {
                var json = EftBridge.GetJson("/sptstalker/anomalies");
                layout = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<AnomalyLayout>(json);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("SPT-Stalker: could not get anomalies from the server (is the server mod installed?): " + e.Message);
                return;
            }

            var location = EftBridge.LocationId(world).ToLowerInvariant();
            if (layout == null)
            {
                return;
            }

            if (!string.Equals(layout.location, location, StringComparison.OrdinalIgnoreCase))
            {
                Plugin.Log.LogWarning($"SPT-Stalker: anomaly layout is for '{layout.location}' but this raid is '{location}', skipping");
                return;
            }

            _root = new GameObject("SPTStalker.Anomalies");

            // Radiation runs on every map (carried artifacts irradiate you even without anomalies)
            if (layout.radiation != null && layout.radiation.enabled)
            {
                _radiation = new RadiationSystem(layout.radiation, layout.artifacts, _root);
            }

            if (layout.fields == null || layout.fields.Length == 0)
            {
                Plugin.Log.LogInfo($"SPT-Stalker: no anomalies on {location} this raid");
                return;
            }

            var effects = (layout.types ?? Array.Empty<AnomalyTypeData>()).Where(t => t.effect != null).ToDictionary(t => t.key, t => t.effect);
            var seed = 1;
            foreach (var field in layout.fields)
            {
                if (!effects.TryGetValue(field.type, out var effect) || field.anomalies == null)
                {
                    continue;
                }

                foreach (var a in field.anomalies)
                {
                    _anomalies.Add(new Anomaly(_root.transform, field.type, effect, a.position, a.radius, seed++));
                }
            }

            _detector = _root.AddComponent<AudioSource>();
            _detector.clip = ProceduralAudio.Beep();
            _detector.spatialBlend = 0f;
            _detector.playOnAwake = false;

            Plugin.Log.LogInfo($"SPT-Stalker: {layout.fields.Length} anomaly fields ({_anomalies.Count} anomalies) on {location}");
        }

        private void UpdateDetector(float edgeDistance, float dt)
        {
            if (!Plugin.DetectorEnabled.Value || _detector == null)
            {
                return;
            }

            var range = Plugin.DetectorRange.Value;
            if (edgeDistance > range)
            {
                _beepTimer = 0;
                return;
            }

            _beepTimer -= dt;
            if (_beepTimer > 0)
            {
                return;
            }

            // 1 beep/second at the edge of range, frantic when you're about to step in
            _beepTimer = Mathf.Lerp(0.08f, 1.0f, edgeDistance / range);
            _detector.PlayOneShot(_detector.clip, Plugin.DetectorVolume.Value);
        }

        private void Cleanup()
        {
            foreach (var anomaly in _anomalies)
            {
                anomaly.Destroy();
            }

            _anomalies.Clear();
            _radiation?.Dispose();
            _radiation = null;
            if (_root != null)
            {
                Destroy(_root);
            }

            _root = null;
            _detector = null;
            _world = null;
            _built = false;
        }
    }
}
