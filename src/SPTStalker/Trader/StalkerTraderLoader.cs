using System.Reflection;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Routers;
using SPTarkov.Server.Core.Utils.Cloners;

namespace SPTStalker.Trader;

/// <summary>
/// Registers the Zone trader with the server.
/// Runs right after the vanilla traders are registered and before the server's own trader
/// and flea-market start-up, so restock timers and flea listings include this trader.
/// </summary>
[Injectable(TypePriority = OnLoadOrder.TraderRegistration + 1)]
public class StalkerTraderLoader(
    ISptLogger<StalkerTraderLoader> logger,
    ModHelper modHelper,
    ImageRouter imageRouter,
    TraderConfig traderConfig,
    RagfairConfig ragfairConfig,
    TradersTable tradersTable,
    LocaleTable localeTable,
    ICloner cloner,
    TraderAssortBuilder assortBuilder) : IOnLoad
{
    private const string DataFolder = "data/trader";

    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        var modPath = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());

        var traderBase = modHelper.GetJsonDataFromFile<TraderBase>(modPath, $"{DataFolder}/base.json");
        var settings = modHelper.GetJsonDataFromFile<TraderSettings>(modPath, $"{DataFolder}/settings.json");
        var assortFile = modHelper.GetJsonDataFromFile<AssortFile>(modPath, $"{DataFolder}/assort.json");

        if (tradersTable.ContainsKey(traderBase.Id))
        {
            logger.Error($"[SPT-Stalker] A trader with id {traderBase.Id} already exists, the Zone trader was not added");
            return Task.CompletedTask;
        }

        // Avatar: base.json "avatar" is "/files/trader/avatar/<name>.jpg"; the route key has no extension
        var avatarFile = Path.Combine(modPath, DataFolder, "avatar.jpg");
        imageRouter.AddRoute(traderBase.Avatar!.Replace(".jpg", ""), avatarFile);

        // Restock timer
        traderConfig.UpdateTime.Add(new UpdateTime
        {
            Name = "SPT-Stalker trader",
            TraderId = traderBase.Id,
            Seconds = new MinMax<int>(settings.RestockMinutesMin * 60, Math.Max(settings.RestockMinutesMin, settings.RestockMinutesMax) * 60)
        });

        ragfairConfig.Traders[traderBase.Id] = settings.ListOffersOnFlea;

        var assort = assortBuilder.Build(traderBase.Id, assortFile);

        tradersTable[traderBase.Id] = new SPTarkov.Server.Core.Models.Eft.Common.Tables.Trader
        {
            Base = cloner.Clone(traderBase)!,
            Assort = assort,
            // Filled in by the quest stage of the mod later
            QuestAssort = new Dictionary<string, Dictionary<MongoId, MongoId>>
            {
                { "Started", new Dictionary<MongoId, MongoId>() },
                { "Success", new Dictionary<MongoId, MongoId>() },
                { "Fail", new Dictionary<MongoId, MongoId>() }
            },
            Dialogue = []
        };

        AddLocales(traderBase.Id, settings);

        var offerCount = assort.LoyalLevelItems.Count;
        var name = settings.Locales.GetValueOrDefault(settings.DefaultLocale)?.Nickname ?? traderBase.Nickname;
        logger.Success($"[SPT-Stalker] Trader '{name}' is open for business ({offerCount} offers)");

        return Task.CompletedTask;
    }

    private void AddLocales(MongoId traderId, TraderSettings settings)
    {
        if (!settings.Locales.TryGetValue(settings.DefaultLocale, out var fallback))
        {
            fallback = settings.Locales.Values.FirstOrDefault() ?? new TraderLocale();
        }

        var id = traderId.ToString();

        foreach (var (language, lazyLocale) in localeTable.Global)
        {
            var text = settings.Locales.GetValueOrDefault(language) ?? fallback;

            // Locales are lazy loaded, so add our text through a transformer that runs on load
            lazyLocale.AddTransformer(locale =>
            {
                if (locale is null)
                {
                    return locale;
                }

                locale[$"{id} FullName"] = text.FullName;
                locale[$"{id} FirstName"] = text.FirstName;
                locale[$"{id} Nickname"] = text.Nickname;
                locale[$"{id} Location"] = text.Location;
                locale[$"{id} Description"] = text.Description;
                return locale;
            });
        }
    }
}
