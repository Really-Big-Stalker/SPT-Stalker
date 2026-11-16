using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Utils.Cloners;
using SPTStalker.Common;

namespace SPTStalker.Trader;

/// <summary>
/// Turns the hand-editable data/trader/assort.json into the server's TraderAssort format.
/// Bad entries are logged and skipped so one typo never stops the trader from loading.
/// </summary>
[Injectable]
public class TraderAssortBuilder(
    ISptLogger<TraderAssortBuilder> logger,
    TemplateTable templateTable,
    GlobalTable globalTable,
    ICloner cloner)
{
    private static readonly Dictionary<string, MongoId> Currencies = new(StringComparer.OrdinalIgnoreCase)
    {
        ["RUB"] = Money.ROUBLES,
        ["USD"] = Money.DOLLARS,
        ["EUR"] = Money.EUROS
    };

    public TraderAssort Build(MongoId traderId, AssortFile assortFile)
    {
        var assort = new TraderAssort
        {
            Items = [],
            BarterScheme = new Dictionary<MongoId, List<List<BarterScheme>>>(),
            LoyalLevelItems = new Dictionary<MongoId, int>()
        };

        var seenKeys = new HashSet<string>();

        foreach (var offer in assortFile.Offers)
        {
            if (!offer.Enabled)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(offer.Key) || !seenKeys.Add(offer.Key))
            {
                logger.Warning($"[SPT-Stalker] Skipping offer '{offer.Name}': key is missing or used twice ('{offer.Key}')");
                continue;
            }

            if (!TryParseTpl(offer.Tpl, out var tpl) || !templateTable.Items.ContainsKey(tpl))
            {
                logger.Warning($"[SPT-Stalker] Skipping offer '{offer.Key}': unknown item tpl '{offer.Tpl}'");
                continue;
            }

            var paymentOptions = BuildPayment(offer);
            if (paymentOptions is null)
            {
                continue;
            }

            var items = BuildOfferItems(traderId, offer.Key, tpl);
            var root = items[0];

            root.ParentId = "hideout";
            root.SlotId = "hideout";
            root.Upd ??= new Upd();
            root.Upd.UnlimitedCount = offer.Unlimited;
            root.Upd.StackObjectsCount = offer.Unlimited ? 999999 : Math.Max(1, offer.Stock);
            if (offer.BuyLimit > 0)
            {
                root.Upd.BuyRestrictionMax = offer.BuyLimit;
                root.Upd.BuyRestrictionCurrent = 0;
            }

            assort.Items.AddRange(items);
            assort.BarterScheme[root.Id] = [paymentOptions];
            assort.LoyalLevelItems[root.Id] = Math.Clamp(offer.LoyaltyLevel, 1, 4);
        }

        return assort;
    }

    /// <summary>
    /// A single item, or the game's default preset (with all its attachments / armour plates)
    /// when the item has one. Every id is derived from the trader id + offer key so it is stable.
    /// </summary>
    private List<Item> BuildOfferItems(MongoId traderId, string offerKey, MongoId tpl)
    {
        var rootId = StableId.From(traderId.ToString(), offerKey);

        // Same rule SPT uses for "default" presets: the preset that has an encyclopedia entry.
        var preset = globalTable.ItemPresets.Values.FirstOrDefault(p =>
            p.Encyclopedia is not null && p.Items.Count > 0 && p.Items[0].Template == tpl);

        if (preset is null)
        {
            return [new Item { Id = rootId, Template = tpl }];
        }

        var items = cloner.Clone(preset.Items)!;

        // Re-id every item and point children at their parent's new id
        var idMap = new Dictionary<string, MongoId>();
        for (var i = 0; i < items.Count; i++)
        {
            idMap[items[i].Id.ToString()] = i == 0 ? rootId : StableId.From(traderId.ToString(), offerKey, i.ToString());
        }

        foreach (var item in items)
        {
            item.Id = idMap[item.Id.ToString()];
            if (item.ParentId is not null && idMap.TryGetValue(item.ParentId, out var newParent))
            {
                item.ParentId = newParent.ToString();
            }
        }

        return items;
    }

    private List<BarterScheme>? BuildPayment(OfferDefinition offer)
    {
        if (offer.Price is not null && offer.Barter is { Count: > 0 })
        {
            logger.Warning($"[SPT-Stalker] Skipping offer '{offer.Key}': set either 'price' or 'barter', not both");
            return null;
        }

        if (offer.Price is not null)
        {
            var currencyKnown = Currencies.TryGetValue(offer.Price.Currency, out var currency);

            if (!currencyKnown || offer.Price.Amount <= 0)
            {
                logger.Warning($"[SPT-Stalker] Skipping offer '{offer.Key}': bad price ({offer.Price.Currency} {offer.Price.Amount})");
                return null;
            }

            return [new BarterScheme { Template = currency, Count = offer.Price.Amount }];
        }

        if (offer.Barter is { Count: > 0 })
        {
            var scheme = new List<BarterScheme>();
            foreach (var part in offer.Barter)
            {
                if (!TryParseTpl(part.Tpl, out var partTpl) || !templateTable.Items.ContainsKey(partTpl) || part.Count <= 0)
                {
                    logger.Warning($"[SPT-Stalker] Skipping offer '{offer.Key}': bad barter item '{part.Name ?? part.Tpl}'");
                    return null;
                }

                scheme.Add(new BarterScheme { Template = partTpl, Count = part.Count });
            }

            return scheme;
        }

        logger.Warning($"[SPT-Stalker] Skipping offer '{offer.Key}': it has no 'price' or 'barter'");
        return null;
    }

    private static bool TryParseTpl(string value, out MongoId tpl)
    {
        tpl = default;
        if (!MongoId.IsValidMongoId(value))
        {
            return false;
        }

        tpl = new MongoId(value);
        return true;
    }
}
