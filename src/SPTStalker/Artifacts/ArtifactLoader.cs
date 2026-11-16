using System.Reflection;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Services.Modding.Custom;

namespace SPTStalker.Artifacts;

/// <summary>
/// Creates the artifact items and adds them to raid loot containers (stashes, caches, dead scavs...).
/// Runs just before the trader is registered so everything later in start-up (flea prices,
/// contracts) already knows the artifacts.
/// </summary>
[Injectable(TypePriority = OnLoadOrder.TraderRegistration - 1)]
public class ArtifactLoader(
    ISptLogger<ArtifactLoader> logger,
    ModHelper modHelper,
    CustomItemService customItemService,
    TemplateTable templateTable,
    LocationTable locationTable,
    ArtifactRegistry registry) : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        var modPath = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        var file = modHelper.GetJsonDataFromFile<ArtifactFile>(modPath, "data/artifacts/artifacts.json");

        foreach (var artifact in file.Artifacts.Where(a => a.Enabled))
        {
            if (TryCreate(artifact, out var tpl))
            {
                registry.Add(new RegisteredArtifact(tpl, artifact));
            }
        }

        if (file.Spawns.Enabled && registry.All.Count > 0)
        {
            AddToLoot(file);
        }

        logger.Success($"[SPT-Stalker] {registry.All.Count} artifacts registered");
        return Task.CompletedTask;
    }

    private bool TryCreate(ArtifactDefinition artifact, out MongoId tpl)
    {
        tpl = default;
        if (!MongoId.IsValidMongoId(artifact.Id) || !MongoId.IsValidMongoId(artifact.PlaceholderTpl))
        {
            logger.Warning($"[SPT-Stalker] Artifact '{artifact.Key}' has an invalid id or placeholderTpl, skipped");
            return false;
        }

        tpl = new MongoId(artifact.Id);
        var placeholder = new MongoId(artifact.PlaceholderTpl);

        if (templateTable.Items.ContainsKey(tpl))
        {
            logger.Warning($"[SPT-Stalker] Artifact '{artifact.Key}' id {artifact.Id} is already used by another item, skipped");
            return false;
        }

        if (!templateTable.Items.TryGetValue(placeholder, out var placeholderItem))
        {
            logger.Warning($"[SPT-Stalker] Artifact '{artifact.Key}' placeholder item {artifact.PlaceholderTpl} does not exist, skipped");
            return false;
        }

        // Artifacts sit in the same handbook category as their placeholder item
        var handbookParent = templateTable.Handbook.Items.FirstOrDefault(h => h.Id == placeholder)?.ParentId.ToString();
        if (handbookParent is null)
        {
            logger.Warning($"[SPT-Stalker] Artifact '{artifact.Key}' placeholder item {artifact.PlaceholderTpl} has no handbook entry, skipped");
            return false;
        }

        var props = new TemplateItemProperties
        {
            Width = 1,
            Height = 1,
            BackgroundColor = artifact.BackgroundColor,
            Weight = artifact.WeightKg,
            ExaminedByDefault = false,
            CanSellOnRagfair = true,
            QuestItem = false
        };

        if (!string.IsNullOrWhiteSpace(artifact.PrefabPath))
        {
            props.Prefab = new Prefab { Path = artifact.PrefabPath, Rcid = "" };
        }

        var result = customItemService.CreateItemFromClone(new NewItemFromCloneDetails
        {
            ItemTplToClone = placeholder,
            ParentId = placeholderItem.Parent,
            NewId = tpl,
            NewItemName = $"sptstalker_artifact_{artifact.Key}",
            FleaPriceRoubles = artifact.PriceRoubles,
            HandbookPriceRoubles = artifact.PriceRoubles,
            HandbookParentId = handbookParent,
            AddToWeaponShelf = false,
            OverrideProperties = props,
            Locales = artifact.Locales.ToDictionary(
                kv => kv.Key,
                kv => new LocaleDetails { Name = kv.Value.Name, ShortName = kv.Value.ShortName, Description = kv.Value.Description })
        });

        if (!result.Success)
        {
            logger.Warning($"[SPT-Stalker] Artifact '{artifact.Key}' could not be created: {string.Join(", ", result.Errors)}");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Adds every artifact to the item pool of the configured loot containers on the configured maps.
    /// Weights are scaled from each container's existing pool so the configured percent is respected.
    /// </summary>
    private void AddToLoot(ArtifactFile file)
    {
        var rarityTotal = registry.All.Sum(a => RarityWeight(file, a.Definition));
        if (rarityTotal <= 0)
        {
            return;
        }

        var maps = file.Spawns.Maps.Select(m => m.ToLowerInvariant()).ToHashSet();

        foreach (var (mapId, location) in locationTable.GetDictionary())
        {
            if (location?.StaticLoot is null || (maps.Count > 0 && !maps.Contains(mapId.ToLowerInvariant())))
            {
                continue;
            }

            location.StaticLoot.AddTransformer(staticLoot =>
            {
                if (staticLoot is null)
                {
                    return staticLoot;
                }

                foreach (var (containerTpl, chancePercent) in file.Spawns.Containers)
                {
                    if (!MongoId.IsValidMongoId(containerTpl) || chancePercent <= 0
                        || !staticLoot.TryGetValue(new MongoId(containerTpl), out var details))
                    {
                        continue;
                    }

                    var pool = details.ItemDistribution.ToList();
                    var existing = pool.Sum(i => (double)(i.RelativeProbability ?? 0));
                    var chance = Math.Min(chancePercent, 90) / 100d;

                    // weight w such that w / (existing + w) == chance
                    var artifactPoolWeight = existing * chance / (1 - chance);

                    foreach (var artifact in registry.All)
                    {
                        pool.RemoveAll(i => i.Tpl == artifact.Tpl);
                        pool.Add(new ItemDistribution
                        {
                            Tpl = artifact.Tpl,
                            RelativeProbability = (float)Math.Max(1, artifactPoolWeight * RarityWeight(file, artifact.Definition) / rarityTotal)
                        });
                    }

                    details.ItemDistribution = pool;
                }

                return staticLoot;
            });
        }
    }

    private static double RarityWeight(ArtifactFile file, ArtifactDefinition artifact)
    {
        return file.RarityWeights.GetValueOrDefault(artifact.Rarity, 1);
    }
}
