using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Generators.RepeatableQuests;
using SPTarkov.Server.Core.Helpers.Quest;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Utils.Json;
using SPTStalker.Artifacts;

namespace SPTStalker.Contracts;

/// <summary>
/// Builds the Bunker's repeatable tasks ("contracts"):
///  - Hunts (replace Elimination): kill a Zone faction, with rolled extra conditions.
///  - Artifact hunts (replace Completion): hand over found-in-raid artifacts.
/// The vanilla generators call into this through <see cref="ContractPatches"/> only when the
/// task is being generated for the Bunker; every other trader is left untouched.
/// </summary>
[Injectable(InjectionType.Singleton)]
public class ContractGenerator(
    ISptLogger<ContractGenerator> logger,
    RepeatableQuestHelper repeatableQuestHelper,
    RepeatableQuestRewardGenerator rewardGenerator,
    TemplateTable templateTable,
    ArtifactRegistry artifactRegistry)
{
    public const string HuntTextPrefix = "sptstalker.contract.hunt";
    public const string ArtifactTextPrefix = "sptstalker.contract.artifact";

    private ContractFile _config = new();
    private MongoId _traderId;
    private readonly Dictionary<string, HashSet<string>> _weaponsByModifier = [];

    public MongoId TraderId => _traderId;

    public void Configure(ContractFile config, MongoId traderId)
    {
        _config = config;
        _traderId = traderId;

        // Resolve weapon class modifiers to concrete weapon templates once
        foreach (var modifier in config.Modifiers.Where(m => m.Type == "weaponClass"))
        {
            var classes = modifier.WeaponBaseClasses.Where(MongoId.IsValidMongoId).Select(c => new MongoId(c)).ToHashSet();
            var weapons = templateTable.Items.Values
                .Where(i => i.Type == "Item" && classes.Contains(i.Parent))
                .Select(i => i.Id.ToString())
                .ToHashSet();
            _weaponsByModifier[modifier.Key] = weapons;
        }
    }

    // ------------------------------------------------------------------ hunts

    public RepeatableQuest? GenerateHunt(MongoId sessionId, int pmcLevel, RepeatableQuestConfig repeatableConfig)
    {
        var group = repeatableConfig.Name;
        var factions = _config.Factions.Where(f => f.Enabled && f.MinLevel <= pmcLevel && f.Texts.Count > 0).ToList();
        var faction = PickWeighted(factions, f => f.Weight);
        if (faction is null)
        {
            return null;
        }

        var levelConfig = repeatableQuestHelper.GetEliminationConfigByPmcLevel(pmcLevel, repeatableConfig);
        if (levelConfig is null)
        {
            return null;
        }

        var quest = repeatableQuestHelper.GenerateRepeatableTemplate(RepeatableQuestType.Elimination, _traderId, repeatableConfig.Side, sessionId);
        if (quest?.Conditions.AvailableForFinish is not { Count: > 0 })
        {
            return null;
        }

        // Kill count
        var range = _config.Hunts.Kills.GetValueOrDefault(group) ?? [3, 6];
        var kills = Math.Max(1, (int)Math.Round(Random.Shared.Next(range[0], range[1] + 1) * faction.KillMultiplier));

        // Map
        // A faction that only lives on some maps is always map-locked; one listed with "any" only sometimes
        var mapKey = "any";
        var maps = faction.Maps.Where(m => m != "any").ToList();
        var mustLock = maps.Count > 0 && !faction.Maps.Contains("any");
        if (maps.Count > 0 && (mustLock || Chance(_config.Hunts.MapLockChancePercent)))
        {
            mapKey = maps[Random.Shared.Next(maps.Count)];
        }

        var kill = new QuestConditionCounterCondition
        {
            Id = new MongoId(),
            DynamicLocale = true,
            Target = new ListOrT<string>(null, faction.Target),
            Value = 1,
            ResetOnSessionEnd = false,
            EnemyHealthEffects = [],
            Daytime = new DaytimeCounter { From = 0, To = 0 },
            ConditionType = "Kills"
        };
        if (faction.SavageRoles.Count > 0)
        {
            kill.SavageRole = [.. faction.SavageRoles];
        }

        var counterConditions = new List<QuestConditionCounterCondition>();
        if (mapKey != "any")
        {
            counterConditions.Add(new QuestConditionCounterCondition
            {
                Id = new MongoId(),
                DynamicLocale = true,
                Target = new ListOrT<string>(LocationNames(mapKey, repeatableConfig), null),
                ConditionType = "Location"
            });
        }

        counterConditions.Add(kill);

        // Extra conditions
        var difficulty = faction.Difficulty;
        var maxModifiers = _config.Hunts.MaxModifiers.GetValueOrDefault(group, 1);
        var chosen = new List<ModifierDefinition>();
        foreach (var modifier in _config.Modifiers.OrderBy(_ => Random.Shared.Next()))
        {
            if (chosen.Count >= maxModifiers)
            {
                break;
            }

            if (!modifier.Enabled || modifier.MinLevel > pmcLevel || faction.BlockedModifiers.Contains(modifier.Key)
                || chosen.Any(c => c.Excludes.Contains(modifier.Key) || modifier.Excludes.Contains(c.Key))
                || !Chance(modifier.ChancePercent))
            {
                continue;
            }

            if (ApplyModifier(modifier, kill, counterConditions))
            {
                chosen.Add(modifier);
                difficulty += modifier.Difficulty;
            }
        }

        var finish = quest.Conditions.AvailableForFinish[0];
        finish.Id = new MongoId();
        finish.Value = kills;
        finish.Counter!.Id = new MongoId();
        finish.Counter.Conditions = counterConditions;

        quest.Location = mapKey == "any" ? "any" : repeatableQuestHelper.GetQuestLocationByMapId(mapKey) ?? "any";

        var textIndex = Random.Shared.Next(faction.Texts.Count);
        ApplyText(quest, $"{HuntTextPrefix}.{faction.Key}.{textIndex}");

        var rewardDifficulty = Math.Clamp(0.25 + difficulty * 0.5 + kills / 40d, 0.2, 1);
        quest.Rewards = rewardGenerator.GenerateReward(pmcLevel, rewardDifficulty, _traderId, repeatableConfig, levelConfig);

        return quest;
    }

    private bool ApplyModifier(ModifierDefinition modifier, QuestConditionCounterCondition kill, List<QuestConditionCounterCondition> counterConditions)
    {
        switch (modifier.Type)
        {
            case "daytime":
                kill.Daytime = new DaytimeCounter { From = modifier.From, To = modifier.To };
                return true;

            case "bodyPart" when modifier.BodyParts.Count > 0 && kill.BodyPart is null:
                kill.BodyPart = [.. modifier.BodyParts];
                return true;

            case "distance" when kill.Distance is null:
                kill.Distance = new CounterConditionDistance { CompareMethod = modifier.CompareMethod, Value = modifier.Value };
                return true;

            case "caliber" when modifier.Calibers.Count > 0 && kill.Weapon is null && kill.WeaponCaliber is null:
                kill.WeaponCaliber = [.. modifier.Calibers];
                return true;

            case "weaponClass" when kill.Weapon is null && kill.WeaponCaliber is null:
                if (!_weaponsByModifier.TryGetValue(modifier.Key, out var weapons) || weapons.Count == 0)
                {
                    return false;
                }

                kill.Weapon = [.. weapons];
                return true;

            case "wearing" when modifier.EquipmentAnyOf.Count > 0:
                counterConditions.Add(new QuestConditionCounterCondition
                {
                    Id = new MongoId(),
                    DynamicLocale = true,
                    EquipmentInclusive = modifier.EquipmentAnyOf.Select(tpl => new List<string> { tpl }).ToList(),
                    ConditionType = "Equipment"
                });
                return true;

            case "survive":
                counterConditions.Add(new QuestConditionCounterCondition
                {
                    Id = new MongoId(),
                    DynamicLocale = true,
                    Status = ["Survived"],
                    ConditionType = "ExitStatus"
                });
                return true;

            default:
                return false;
        }
    }

    // --------------------------------------------------------- artifact hunts

    public RepeatableQuest? GenerateArtifactHunt(MongoId sessionId, int pmcLevel, RepeatableQuestConfig repeatableConfig)
    {
        var settings = _config.ArtifactHunts;
        if (!settings.Enabled || settings.Texts.Count == 0)
        {
            return null;
        }

        var maxTier = settings.TierByLevel.Where(t => t.Length == 2 && pmcLevel >= t[0]).Select(t => t[1]).DefaultIfEmpty(1).Max();
        var candidates = artifactRegistry.All.Where(a => a.Definition.Tier <= maxTier).ToList();
        if (candidates.Count == 0)
        {
            logger.Debug("[SPT-Stalker] No artifacts available for an artifact contract");
            return null;
        }

        var levelConfig = repeatableQuestHelper.GetCompletionConfigByPmcLevel(pmcLevel, repeatableConfig);
        if (levelConfig is null)
        {
            return null;
        }

        var quest = repeatableQuestHelper.GenerateRepeatableTemplate(RepeatableQuestType.Completion, _traderId, repeatableConfig.Side, sessionId);
        if (quest is null)
        {
            return null;
        }

        var group = repeatableConfig.Name;
        var countRange = settings.Count.GetValueOrDefault(group) ?? [1, 2];
        var total = Random.Shared.Next(countRange[0], countRange[1] + 1);
        var typeCount = Math.Clamp(Random.Shared.Next(1, settings.MaxTypes.GetValueOrDefault(group, 1) + 1), 1, Math.Min(total, candidates.Count));

        // Prefer common artifacts: weight by inverse tier
        var picked = new List<RegisteredArtifact>();
        var pool = candidates.ToList();
        for (var i = 0; i < typeCount; i++)
        {
            var pick = PickWeighted(pool, a => 1d / a.Definition.Tier);
            if (pick is null)
            {
                break;
            }

            picked.Add(pick);
            pool.Remove(pick);
        }

        // Split the total between the picked types (at least 1 each)
        var counts = picked.ToDictionary(p => p, _ => 1);
        for (var i = picked.Count; i < total; i++)
        {
            counts[picked[Random.Shared.Next(picked.Count)]]++;
        }

        quest.Conditions.AvailableForFinish = [];
        var index = 0;
        foreach (var (artifact, count) in counts)
        {
            quest.Conditions.AvailableForFinish.Add(new QuestCondition
            {
                Id = new MongoId(),
                Index = index++,
                ParentId = string.Empty,
                DynamicLocale = true,
                VisibilityConditions = [],
                Target = new ListOrT<string>([artifact.Tpl.ToString()], null),
                Value = count,
                MinDurability = 0,
                MaxDurability = 100,
                DogtagLevel = 0,
                OnlyFoundInRaid = true,
                IsEncoded = false,
                ConditionType = "HandoverItem"
            });
        }

        quest.Location = "any";
        ApplyText(quest, $"{ArtifactTextPrefix}.{Random.Shared.Next(settings.Texts.Count)}");

        var tierScore = counts.Sum(kv => kv.Key.Definition.Tier * kv.Value);
        var rewardDifficulty = Math.Clamp(0.3 + tierScore / 12d, 0.3, 1);
        quest.Rewards = rewardGenerator.GenerateReward(pmcLevel, rewardDifficulty, _traderId, repeatableConfig, levelConfig);

        return quest;
    }

    // ---------------------------------------------------------------- helpers

    /// <summary>Points every text field of the quest at this mod's locale keys (registered by ContractLoader).</summary>
    private static void ApplyText(RepeatableQuest quest, string key)
    {
        quest.Name = $"{key} name";
        quest.Description = $"{key} description";
        quest.Note = $"{key} description";
        quest.StartedMessageText = $"{key} description";
        quest.SuccessMessageText = $"{key} success";
        quest.FailMessageText = $"{key} fail";
        quest.ChangeQuestMessageText = $"{key} fail";
        quest.AcceptPlayerMessage = $"{key} empty";
        quest.DeclinePlayerMessage = $"{key} empty";
        quest.CompletePlayerMessage = $"{key} empty";
    }

    private static List<string> LocationNames(string mapKey, RepeatableQuestConfig repeatableConfig)
    {
        if (Enum.TryParse<ELocationName>(mapKey, out var locationName)
            && repeatableConfig.Locations.TryGetValue(locationName, out var names))
        {
            return names;
        }

        return [mapKey];
    }

    private static bool Chance(double percent) => Random.Shared.NextDouble() * 100 < percent;

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
