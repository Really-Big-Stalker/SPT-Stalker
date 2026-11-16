using System.Reflection;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTStalker.Common;

namespace SPTStalker.Contracts;

/// <summary>
/// Puts the Bunker on the Daily/Weekly task-giver lists, registers the contract texts and
/// switches on the generator hooks.
/// </summary>
[Injectable(TypePriority = OnLoadOrder.TraderRegistration + 2)]
public class ContractLoader(
    ISptLogger<ContractLoader> logger,
    ModHelper modHelper,
    QuestConfig questConfig,
    LocaleWriter localeWriter,
    ContractGenerator generator) : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        var modPath = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        var config = modHelper.GetJsonDataFromFile<ContractFile>(modPath, "data/contracts/contracts.json");
        if (!config.Enabled)
        {
            return Task.CompletedTask;
        }

        var traderBase = modHelper.GetJsonDataFromFile<TraderBase>(modPath, "data/trader/base.json");
        generator.Configure(config, traderBase.Id);

        var questTypes = new HashSet<string>();
        if (config.Factions.Any(f => f.Enabled))
        {
            questTypes.Add("Elimination");
        }

        if (config.ArtifactHunts.Enabled)
        {
            questTypes.Add("Completion");
        }

        foreach (var repeatable in questConfig.RepeatableQuests)
        {
            var weight = config.TraderWeight.GetValueOrDefault(repeatable.Name, 0);
            for (var i = 0; i < weight; i++)
            {
                repeatable.TraderWhitelist.Add(new TraderWhitelist
                {
                    TraderId = traderBase.Id,
                    Name = "sptstalker_bunker",
                    QuestTypes = questTypes,
                    RewardBaseWhitelist = config.RewardBaseWhitelist.Where(MongoId.IsValidMongoId).Select(id => new MongoId(id)).ToList(),
                    RewardCanBeWeapon = config.RewardCanBeWeapon,
                    WeaponRewardChancePercent = config.WeaponRewardChancePercent
                });
            }
        }

        localeWriter.AddForAllLanguages(BuildTexts(config));
        ContractPatches.Enable(generator);

        logger.Success($"[SPT-Stalker] Zone contracts ready ({config.Factions.Count(f => f.Enabled)} factions, {config.Modifiers.Count(m => m.Enabled)} modifiers)");
        return Task.CompletedTask;
    }

    private static Dictionary<string, string> BuildTexts(ContractFile config)
    {
        var texts = new Dictionary<string, string>();

        void Add(string key, ContractText text)
        {
            texts[$"{key} name"] = text.Name;
            texts[$"{key} description"] = text.Description;
            texts[$"{key} success"] = text.Success;
            texts[$"{key} fail"] = text.Fail;
            texts[$"{key} empty"] = "";
        }

        foreach (var faction in config.Factions)
        {
            for (var i = 0; i < faction.Texts.Count; i++)
            {
                Add($"{ContractGenerator.HuntTextPrefix}.{faction.Key}.{i}", faction.Texts[i]);
            }
        }

        for (var i = 0; i < config.ArtifactHunts.Texts.Count; i++)
        {
            Add($"{ContractGenerator.ArtifactTextPrefix}.{i}", config.ArtifactHunts.Texts[i]);
        }

        return texts;
    }
}
