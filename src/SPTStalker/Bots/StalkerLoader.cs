using System.Reflection;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace SPTStalker.Bots;

/// <summary>
/// Turns the unused stalker role into roaming Loner groups: names and gear, group spawns on each map, and
/// who they like and hate. Their roaming behaviour comes from the SPTStalker.Bots client plugin (BigBrain).
/// </summary>
[Injectable(TypePriority = OnLoadOrder.TraderRegistration + 4)]
public class StalkerLoader(
    ISptLogger<StalkerLoader> logger,
    ModHelper modHelper,
    BotTable botTable,
    TemplateTable templateTable,
    LocationTable locationTable) : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        var modPath = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        var config = modHelper.GetJsonDataFromFile<StalkerFile>(modPath, "data/bots/stalkers.json");
        if (!config.Enabled)
        {
            return Task.CompletedTask;
        }

        if (!botTable.Types.TryGetValue(config.Role.ToLowerInvariant(), out var botType) || botType is null)
        {
            logger.Error($"[SPT-Stalker] Bot role '{config.Role}' has no bot data, stalkers disabled");
            return Task.CompletedTask;
        }

        ApplyIdentityAndLoadout(config, botType);
        var groups = AddSpawnsAndHostility(config);

        logger.Success($"[SPT-Stalker] Loner stalkers ready (role {config.Role}, up to {groups} groups across all maps). " +
                       "Install SPTStalker.Bots + BigBrain for their roaming behaviour.");
        return Task.CompletedTask;
    }

    // ------------------------------------------------------------------ identity + gear

    private void ApplyIdentityAndLoadout(StalkerFile config, BotType botType)
    {
        if (config.Names.Count > 0)
        {
            botType.FirstNames = config.Names.ToList();
            botType.LastNames = [];
        }

        var inventory = botType.BotInventory;
        var sources = config.Loadout.SourceRoles
            .Select(r => botTable.Types.GetValueOrDefault(r.ToLowerInvariant()))
            .Where(t => t?.BotInventory is not null)
            .Select(t => t!.BotInventory)
            .ToList();

        // Attachment / plate / ammo pools: ours + every source role's, so any listed item can be fully built
        inventory.Mods ??= new Dictionary<MongoId, Dictionary<string, HashSet<MongoId>>>();
        inventory.Ammo ??= new Dictionary<string, Dictionary<MongoId, double>>();
        foreach (var source in sources)
        {
            foreach (var (parent, slots) in source.Mods ?? [])
            {
                if (!inventory.Mods.TryGetValue(parent, out var ourSlots))
                {
                    inventory.Mods[parent] = ourSlots = new Dictionary<string, HashSet<MongoId>>();
                }

                foreach (var (slot, tpls) in slots)
                {
                    if (!ourSlots.TryGetValue(slot, out var set))
                    {
                        ourSlots[slot] = set = [];
                    }

                    set.UnionWith(tpls);
                }
            }

            foreach (var (caliber, pool) in source.Ammo ?? [])
            {
                if (!inventory.Ammo.TryGetValue(caliber, out var ourPool))
                {
                    inventory.Ammo[caliber] = ourPool = new Dictionary<MongoId, double>();
                }

                foreach (var (tpl, weight) in pool)
                {
                    ourPool.TryAdd(tpl, weight);
                }
            }
        }

        foreach (var (slotName, items) in config.Loadout.Equipment)
        {
            if (!Enum.TryParse<EquipmentSlots>(slotName, true, out var slot))
            {
                logger.Warning($"[SPT-Stalker] Unknown equipment slot '{slotName}' in stalkers.json");
                continue;
            }

            var pool = new Dictionary<MongoId, double>();
            foreach (var (tplString, weight) in items)
            {
                if (!MongoId.IsValidMongoId(tplString))
                {
                    continue;
                }

                var tpl = new MongoId(tplString);
                if (!templateTable.Items.TryGetValue(tpl, out var template))
                {
                    logger.Warning($"[SPT-Stalker] Stalker loadout item {tplString} does not exist, skipped");
                    continue;
                }

                // Items with slots (weapons, plate carriers, helmets) need attachment data to be generated
                var needsMods = template.Properties?.Slots?.Any(s => s.Required == true) == true;
                if (needsMods && !inventory.Mods.ContainsKey(tpl))
                {
                    logger.Warning($"[SPT-Stalker] Stalker loadout item {template.Name} has no attachment data in the source roles, skipped");
                    continue;
                }

                pool[tpl] = weight;
            }

            if (pool.Count > 0)
            {
                inventory.Equipment[slot] = pool;
            }
        }

        foreach (var (slot, chance) in config.Loadout.EquipmentChances)
        {
            botType.BotChances.EquipmentChances[slot] = chance;
        }
    }

    // ------------------------------------------------------------------ spawns + hostility

    private int AddSpawnsAndHostility(StalkerFile config)
    {
        var total = 0;
        foreach (var (mapId, location) in locationTable.GetDictionary())
        {
            var locationBase = location?.Base;
            if (locationBase is null || string.IsNullOrEmpty(locationBase.OpenZones))
            {
                continue;
            }

            var spawn = config.Spawns.FirstOrDefault(s => s.Key.Equals(mapId, StringComparison.OrdinalIgnoreCase)).Value
                        ?? config.Spawns.GetValueOrDefault("default");
            if (spawn is null || spawn.Groups <= 0 || spawn.ChancePercent <= 0)
            {
                continue;
            }

            var min = Math.Max(1, spawn.GroupSize[0]);
            var max = Math.Max(min, spawn.GroupSize[^1]);
            var escorts = string.Join(",", Enumerable.Range(min - 1, max - min + 1));

            locationBase.BossLocationSpawn ??= [];
            for (var i = 0; i < spawn.Groups; i++)
            {
                locationBase.BossLocationSpawn.Add(new BossLocationSpawn
                {
                    BossName = config.Role,
                    BossChance = spawn.ChancePercent,
                    BossZone = locationBase.OpenZones,
                    IsBossPlayer = false,
                    BossDifficulty = "normal",
                    BossEscortType = config.Role,
                    BossEscortDifficulty = "normal",
                    BossEscortAmount = escorts,
                    Time = spawn.RandomTime ? -1 : spawn.Time,
                    IsRandomTimeSpawn = spawn.RandomTime,
                    Supports = [],
                    TriggerId = "",
                    TriggerName = "",
                    Delay = 0,
                    ForceSpawn = false,
                    IgnoreMaxBots = true,
                    DependKarma = false,
                    DependKarmaPVE = false,
                    ShowOnTarkovMap = false,
                    ShowOnTarkovMapPvE = false,
                    SpawnMode = ["regular", "pve"],
                    SptId = $"sptstalker_loners_{i}"
                });
                total++;
            }

            if (locationBase.BotLocationModifier is not null)
            {
                var hostility = (locationBase.BotLocationModifier.AdditionalHostilitySettings ?? [])
                    .Where(h => !string.Equals(h.BotRole, config.Role, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                hostility.Add(new AdditionalHostilitySettings
                {
                    BotRole = config.Role,
                    AlwaysEnemies = [.. config.Hostility.Enemies],
                    AlwaysFriends = [.. config.Hostility.Friends.Append(config.Role)],
                    Neutral = [],
                    Warn = [],
                    ChancedEnemies = [],
                    BearPlayerBehaviour = config.Hostility.TowardsPlayerPmc,
                    UsecPlayerBehaviour = config.Hostility.TowardsPlayerPmc,
                    SavagePlayerBehaviour = config.Hostility.TowardsPlayerScav,
                    BearEnemyChance = 0,
                    UsecEnemyChance = 0,
                    SavageEnemyChance = 0
                });
                locationBase.BotLocationModifier.AdditionalHostilitySettings = hostility;
            }
        }

        return total;
    }
}
