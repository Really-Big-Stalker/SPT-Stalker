using UnityEngine;
using UnityEngine.AI;

namespace SPTStalker.Client
{
    /// <summary>
    /// Checks a proposed anomaly spot against the actual level and finds a better one nearby if needed, so
    /// anomalies end up in open, reachable places: not under roofs, not in cramped rooms or corridors (where
    /// they could block the way), not on shelves or inside furniture, and always on walkable ground.
    /// </summary>
    internal static class Placement
    {
        private const int Directions = 12;
        private static readonly float[] RayHeights = { 0.4f, 1.4f };

        public static bool FindSpot(Vector3 proposed, Vector3 fieldCenter, float radius, AnomalyPlacementData rules, out Vector3 spot, out string reason)
        {
            reason = "";
            var attempts = Mathf.Max(1, rules.attempts);
            for (var i = 0; i < attempts; i++)
            {
                // First the server's spot, then random spots elsewhere in the field
                var candidate = i == 0
                    ? proposed
                    : fieldCenter + Quaternion.Euler(0, Random.Range(0f, 360f), 0) * Vector3.forward * Random.Range(0f, Mathf.Max(1f, rules.fieldRadius));

                if (IsGood(candidate, radius, rules, out spot, out reason))
                {
                    return true;
                }
            }

            spot = proposed;
            return false;
        }

        private static bool IsGood(Vector3 candidate, float radius, AnomalyPlacementData rules, out Vector3 ground, out string reason)
        {
            ground = candidate;
            reason = "";

            // Drop onto the surface below (from a little above, so we land on the floor/terrain, not a shelf top below eye level)
            if (!Physics.Raycast(candidate + Vector3.up * 3f, Vector3.down, out var hit, 12f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                reason = "no ground";
                return false;
            }

            ground = hit.point;

            // Walkable: on (or right next to) the AI navmesh, so it's real floor/terrain, not furniture
            if (rules.maxNavMeshDistance > 0)
            {
                if (!NavMesh.SamplePosition(ground, out var nav, rules.maxNavMeshDistance, NavMesh.AllAreas))
                {
                    reason = "not walkable";
                    return false;
                }

                ground = nav.position;
            }

            // Open sky: nothing overhead (roof, ceiling, stairs, overhang)
            if (rules.minOpenSky > 0 && Physics.Raycast(ground + Vector3.up * 0.5f, Vector3.up, rules.minOpenSky, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                reason = "under a roof";
                return false;
            }

            // Clearance: enough free space all around so it can't fill a room or block a passage
            var reach = radius + Mathf.Max(0f, rules.clearance);
            var blocked = 0;
            for (var d = 0; d < Directions; d++)
            {
                var dir = Quaternion.Euler(0, d * 360f / Directions, 0) * Vector3.forward;
                foreach (var h in RayHeights)
                {
                    if (Physics.Raycast(ground + Vector3.up * h, dir, reach, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    {
                        blocked++;
                        break;
                    }
                }
            }

            if (blocked > rules.maxBlockedDirections)
            {
                reason = $"cramped ({blocked}/{Directions} directions blocked)";
                return false;
            }

            return true;
        }
    }
}
