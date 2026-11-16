using System.Collections.Concurrent;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTStalker.Artifacts;
using SPTStalker.Common;

namespace SPTStalker.Anomalies;

/// <summary>
/// Rolls a fresh anomaly layout for every raid and keeps it per player session until the client asks for it.
/// Anomaly positions come from the map's loose-loot spawn points (real, reachable ground), so every raid
/// moves the fields around. Fields can hold an artifact of their family, added to the raid's loot.
/// </summary>
[Injectable(InjectionType.Singleton)]
public class AnomalyLayoutService(
    ISptLogger<AnomalyLayoutService> logger,
    LocationTable locationTable,
    ArtifactRegistry artifactRegistry)
{
    private readonly ConcurrentDictionary<MongoId, AnomalyLayout> _layouts = new();
    private AnomalyFile _config = new();
    private List<ArmorClassDto> _armorClasses = [];

    public void Configure(AnomalyFile config, List<ArmorClassDto> armorClasses)
    {
        _config = config;
        _armorClasses = armorClasses;
    }

    public AnomalyLayout GetLayout(MongoId sessionId) =>
        _layouts.TryGetValue(sessionId, out var layout) ? layout : new AnomalyLayout();

    /// <summary>Called when the server builds a raid (location + loot) for a player.</summary>
    public void GenerateForRaid(MongoId sessionId, string locationName, LocationBase raidLocation)
    {
        var mapKey = locationName.ToLowerInvariant();
        var layout = new AnomalyLayout
        {
            Location = mapKey,
            Radiation = _config.Radiation,
            Artifacts = ArtifactRadiation(),
            Armor = _config.Armor,
            ArmorClasses = _armorClasses
        };
        _layouts[sessionId] = layout;

        if (!_config.Enabled || mapKey == "hideout")
        {
            return;
        }

        var settings = _config.Maps.FirstOrDefault(m => m.Key.Equals(mapKey, StringComparison.OrdinalIgnoreCase)).Value
                       ?? _config.Maps.GetValueOrDefault("default")
                       ?? new MapAnomalySettings();

        var fieldCount = Roll(settings.Fields);
        if (fieldCount <= 0)
        {
            return;
        }

        var candidates = CandidatePoints(locationName);
        if (candidates.Count == 0)
        {
            logger.Debug($"[SPT-Stalker] No anomaly positions for {locationName}");
            return;
        }

        var spawns = (raidLocation.SpawnPointParams ?? [])
            .Where(s => s.Position is not null && (s.Categories?.Contains("Player") ?? false))
            .Select(s => s.Position!.Value)
            .ToList();

        var types = _config.Types.Where(t => t.Enabled).ToList();
        if (types.Count == 0)
        {
            return;
        }

        var loot = (raidLocation.Loot ?? []).ToList();
        var minSpawn = _config.MinDistanceFromPlayerSpawn;
        var minField = _config.MinDistanceBetweenFields;

        foreach (var center in candidates.OrderBy(_ => Random.Shared.Next()))
        {
            if (layout.Fields.Count >= fieldCount)
            {
                break;
            }

            if (spawns.Any(s => FlatDistance(s, center) < minSpawn)
                || layout.Fields.Any(f => FlatDistance(f.Center, center) < minField))
            {
                continue;
            }

            var type = PickWeighted(types, t => settings.TypeWeights.GetValueOrDefault(t.Key, t.Weight));
            if (type is null)
            {
                break;
            }

            var field = new AnomalyFieldDto { Type = type.Key, Center = ToVec(center) };
            var count = Math.Max(1, Roll(settings.AnomaliesPerField));
            for (var i = 0; i < count; i++)
            {
                // First anomaly sits on the centre, the rest spread around it; the client snaps them to the ground
                var angle = Random.Shared.NextDouble() * Math.PI * 2;
                var dist = i == 0 ? 0 : (0.35 + 0.65 * Random.Shared.NextDouble()) * settings.FieldRadius;
                field.Anomalies.Add(new AnomalyDto
                {
                    Position = new Vec3
                    {
                        X = center.X + (float)(Math.Cos(angle) * dist),
                        Y = center.Y,
                        Z = center.Z + (float)(Math.Sin(angle) * dist)
                    },
                    Radius = (float)(type.Radius[0] + Random.Shared.NextDouble() * (type.Radius[^1] - type.Radius[0]))
                });
            }

            if (!string.IsNullOrEmpty(type.Family) && Random.Shared.NextDouble() * 100 < settings.ArtifactChancePercent)
            {
                var artifact = PickWeighted(
                    artifactRegistry.All.Where(a => a.Definition.Anomaly.Equals(type.Family, StringComparison.OrdinalIgnoreCase)).ToList(),
                    a => 1d / (a.Definition.Tier * a.Definition.Tier));
                if (artifact is not null)
                {
                    loot.Add(ArtifactLoot(artifact, center));
                    field.Artifact = artifact.Definition.Key;
                }
            }

            layout.Fields.Add(field);
        }

        raidLocation.Loot = loot;
        layout.Types = types.Where(t => layout.Fields.Any(f => f.Type == t.Key))
            .Select(t => new AnomalyTypeDto { Key = t.Key, Effect = t.Effect })
            .ToList();

        logger.Info($"[SPT-Stalker] {locationName}: {layout.Fields.Count} anomaly fields, " +
                    $"{layout.Fields.Count(f => f.Artifact.Length > 0)} with artifacts");
    }

    private List<ArtifactRadiationDto> ArtifactRadiation()
    {
        var byTier = _config.Radiation.ArtifactRadiationByTier;
        return artifactRegistry.All.Select(a => new ArtifactRadiationDto
        {
            Tpl = a.Tpl.ToString(),
            RadiationPerSecond = byTier.Length == 0 ? 0 : byTier[Math.Clamp(a.Definition.Tier, 1, byTier.Length) - 1]
        }).ToList();
    }

    private List<Vector3> CandidatePoints(string locationName)
    {
        var location = locationTable.GetLocation(locationName);
        var spawnpoints = location?.LooseLoot?.Value?.Spawnpoints;
        if (spawnpoints is null)
        {
            return [];
        }

        return spawnpoints
            .Where(s => s.Template?.Position is not null)
            .Select(s => s.Template!.Position!.Value)
            .ToList();
    }

    private static SpawnpointTemplate ArtifactLoot(RegisteredArtifact artifact, Vector3 center)
    {
        // Fresh ids every raid: these are one-off loot items
        var itemId = new MongoId();
        return new SpawnpointTemplate
        {
            Id = $"sptstalker_anomaly_{itemId}",
            IsContainer = false,
            UseGravity = true,
            RandomRotation = true,
            Position = new Vector3(center.X, center.Y + 0.25f, center.Z),
            Rotation = new Vector3(0, 0, 0),
            IsAlwaysSpawn = true,
            IsGroupPosition = false,
            GroupPositions = [],
            Root = itemId.ToString(),
            Items =
            [
                new SptLootItem
                {
                    Id = itemId,
                    Template = artifact.Tpl,
                    Upd = new Upd { StackObjectsCount = 1 }
                }
            ]
        };
    }

    private static double FlatDistance(Vector3 a, Vector3 b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Z - b.Z, 2));

    private static double FlatDistance(Vec3 a, Vector3 b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Z - b.Z, 2));

    private static Vec3 ToVec(Vector3 v) => new() { X = v.X, Y = v.Y, Z = v.Z };

    private static int Roll(int[] range) => range.Length == 0 ? 0 : Random.Shared.Next(range[0], range[^1] + 1);

    private static T? PickWeighted<T>(IReadOnlyList<T> items, Func<T, double> weight) where T : class
    {
        var total = items.Sum(i => Math.Max(0, weight(i)));
        if (total <= 0)
        {
            return null;
        }

        var roll = Random.Shared.NextDouble() * total;
        foreach (var item in items)
        {
            roll -= Math.Max(0, weight(item));
            if (roll <= 0)
            {
                return item;
            }
        }

        return items[^1];
    }
}
