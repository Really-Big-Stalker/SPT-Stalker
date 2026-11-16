using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EFT;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace SPTStalker.Bots
{
    /// <summary>
    /// Where stalker groups go. Mostly to the edge of this raid's anomaly fields (where artifacts form),
    /// asked from the server once per raid; otherwise a random reachable spot some distance away.
    /// </summary>
    internal static class RoamTargets
    {
        private static GameWorld _world;
        private static List<Vector3> _fields = new List<Vector3>();

        public static Vector3 Pick(Vector3 from)
        {
            Refresh();

            if (_fields.Count > 0 && Random.value < Plugin.AnomalyPreference.Value)
            {
                // A field that isn't the one we're standing at
                var candidates = _fields.Where(f => Flat(f, from) > 30f).ToList();
                if (candidates.Count > 0)
                {
                    var field = candidates[Random.Range(0, candidates.Count)];
                    // Stop short of the field, on the side we're coming from: stalkers don't walk into anomalies
                    var dir = (from - field);
                    dir.y = 0;
                    dir = dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.forward;
                    dir = Quaternion.Euler(0, Random.Range(-60f, 60f), 0) * dir;
                    if (Sample(field + dir * Random.Range(10f, 14f), 8f, out var nearField))
                    {
                        return nearField;
                    }
                }
            }

            for (var i = 0; i < 12; i++)
            {
                var dir = Quaternion.Euler(0, Random.Range(0f, 360f), 0) * Vector3.forward;
                if (Sample(from + dir * Random.Range(60f, 160f), 25f, out var spot) && Flat(spot, from) > 40f)
                {
                    return spot;
                }
            }

            return Sample(from + Random.insideUnitSphere * 20f, 10f, out var near) ? near : from;
        }

        private static bool Sample(Vector3 point, float radius, out Vector3 result)
        {
            if (NavMesh.SamplePosition(point, out var hit, radius, NavMesh.AllAreas))
            {
                result = hit.position;
                return true;
            }

            result = point;
            return false;
        }

        /// <summary>Once per raid: fetch the anomaly field centres the server rolled for this map.</summary>
        private static void Refresh()
        {
            if (_world != null)
            {
                return;
            }

            _world = UnityEngine.Object.FindObjectOfType<GameWorld>();
            _fields = new List<Vector3>();
            if (_world == null)
            {
                return;
            }

            try
            {
                var handler = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(a => a.GetType("SPT.Common.Http.RequestHandler", false))
                    .FirstOrDefault(t => t != null);
                var getJson = handler?.GetMethod("GetJson", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
                var json = getJson?.Invoke(null, new object[] { "/sptstalker/anomalies" }) as string;
                var layout = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<Layout>(json);
                if (layout?.fields != null && string.Equals(layout.location, _world.LocationId, StringComparison.OrdinalIgnoreCase))
                {
                    _fields = layout.fields.Select(f => f.center).ToList();
                }

                Plugin.Log.LogInfo($"SPT-Stalker Bots: {_fields.Count} anomaly fields to roam between on {_world.LocationId}");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("SPT-Stalker Bots: could not read anomaly fields: " + e.Message);
            }
        }

        private static float Flat(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

#pragma warning disable 0649
        [Serializable]
        private class Layout
        {
            public string location;
            public Field[] fields;
        }

        [Serializable]
        private class Field
        {
            public Vector3 center;
        }
#pragma warning restore 0649
    }
}
