using System.Text.Json.Serialization;

namespace SPTStalker.Trader;

/// <summary>
/// data/trader/settings.json: how the trader behaves on the server, plus the text shown in game.
/// </summary>
public record TraderSettings
{
    /// <summary>Shortest time, in minutes, between stock refreshes.</summary>
    [JsonPropertyName("restockMinutesMin")]
    public int RestockMinutesMin { get; set; } = 60;

    /// <summary>Longest time, in minutes, between stock refreshes.</summary>
    [JsonPropertyName("restockMinutesMax")]
    public int RestockMinutesMax { get; set; } = 120;

    /// <summary>Whether the trader's stock also shows up on the flea market.</summary>
    [JsonPropertyName("listOffersOnFlea")]
    public bool ListOffersOnFlea { get; set; } = true;

    /// <summary>Text used for every game language unless that language has its own entry in <see cref="Locales"/>.</summary>
    [JsonPropertyName("defaultLocale")]
    public string DefaultLocale { get; set; } = "en";

    /// <summary>Language code (en, ru, de, fr, ...) to the trader's text in that language.</summary>
    [JsonPropertyName("locales")]
    public Dictionary<string, TraderLocale> Locales { get; set; } = [];
}

public record TraderLocale
{
    [JsonPropertyName("fullName")]
    public string FullName { get; set; } = "";

    [JsonPropertyName("firstName")]
    public string FirstName { get; set; } = "";

    [JsonPropertyName("nickname")]
    public string Nickname { get; set; } = "";

    [JsonPropertyName("location")]
    public string Location { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";
}

/// <summary>
/// data/trader/assort.json: what the trader sells. Kept in a simple, hand-editable format;
/// <see cref="TraderAssortBuilder"/> turns it into the server's assort format.
/// </summary>
public record AssortFile
{
    [JsonPropertyName("offers")]
    public List<OfferDefinition> Offers { get; set; } = [];
}

public record OfferDefinition
{
    /// <summary>
    /// Short, unique, never-changing key for the offer (e.g. "pm_pistol").
    /// The offer's in-game id is derived from it, so renaming it resets buy limits for that offer.
    /// </summary>
    [JsonPropertyName("key")]
    public string Key { get; set; } = "";

    /// <summary>Human readable label. Not used by the game.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>Item template id being sold. Weapons and armour use the game's default preset automatically.</summary>
    [JsonPropertyName("tpl")]
    public string Tpl { get; set; } = "";

    [JsonPropertyName("loyaltyLevel")]
    public int LoyaltyLevel { get; set; } = 1;

    /// <summary>How many the trader has per restock. Ignored when <see cref="Unlimited"/> is true.</summary>
    [JsonPropertyName("stock")]
    public int Stock { get; set; } = 10;

    [JsonPropertyName("unlimited")]
    public bool Unlimited { get; set; }

    /// <summary>Max a player may buy per restock. 0 or missing = no limit.</summary>
    [JsonPropertyName("buyLimit")]
    public int BuyLimit { get; set; }

    /// <summary>Money price. Use either this or <see cref="Barter"/>.</summary>
    [JsonPropertyName("price")]
    public PriceDefinition? Price { get; set; }

    /// <summary>Items asked in exchange. Use either this or <see cref="Price"/>.</summary>
    [JsonPropertyName("barter")]
    public List<BarterDefinition>? Barter { get; set; }

    /// <summary>Set false to keep an offer in the file without selling it.</summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;
}

public record PriceDefinition
{
    /// <summary>RUB, USD or EUR.</summary>
    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "RUB";

    [JsonPropertyName("amount")]
    public double Amount { get; set; }
}

public record BarterDefinition
{
    [JsonPropertyName("tpl")]
    public string Tpl { get; set; } = "";

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("count")]
    public double Count { get; set; } = 1;
}
