using System.Text.Json.Serialization;

namespace SPTStalker.Artifacts;

/// <summary>data/artifacts/artifacts.json</summary>
public record ArtifactFile
{
    /// <summary>Rarity name to how likely it is compared with the others (higher = more common).</summary>
    [JsonPropertyName("rarityWeights")]
    public Dictionary<string, double> RarityWeights { get; set; } = [];

    [JsonPropertyName("spawns")]
    public ArtifactSpawnSettings Spawns { get; set; } = new();

    [JsonPropertyName("artifacts")]
    public List<ArtifactDefinition> Artifacts { get; set; } = [];
}

public record ArtifactSpawnSettings
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>Map ids to spawn on (bigmap, woods, shoreline, ...). Empty = every map.</summary>
    [JsonPropertyName("maps")]
    public List<string> Maps { get; set; } = [];

    /// <summary>
    /// Loot container template id to the chance (percent) that any single item rolled in that
    /// container is an artifact. The artifact is then picked by rarity weight.
    /// </summary>
    [JsonPropertyName("containers")]
    public Dictionary<string, double> Containers { get; set; } = [];
}

public record ArtifactDefinition
{
    /// <summary>Permanent unique key, also used in contracts (e.g. "jellyfish").</summary>
    [JsonPropertyName("key")]
    public string Key { get; set; } = "";

    /// <summary>Permanent item id (24 hex chars). Never change it once players have the item.</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("rarity")]
    public string Rarity { get; set; } = "common";

    /// <summary>1 = early game ... 4 = end game. Contracts only ask for tiers the player's level allows.</summary>
    [JsonPropertyName("tier")]
    public int Tier { get; set; } = 1;

    /// <summary>Anomaly family, flavour only for now (gravitational, electro, chemical, thermal, bio).</summary>
    [JsonPropertyName("anomaly")]
    public string Anomaly { get; set; } = "";

    [JsonPropertyName("priceRoubles")]
    public double PriceRoubles { get; set; } = 50000;

    /// <summary>Vanilla item whose model the artifact uses until its own 3D model bundle exists.</summary>
    [JsonPropertyName("placeholderTpl")]
    public string PlaceholderTpl { get; set; } = "";

    /// <summary>Path of the custom model inside a bundle (see bundles.json). Empty = use the placeholder's model.</summary>
    [JsonPropertyName("prefabPath")]
    public string? PrefabPath { get; set; }

    /// <summary>Inventory cell background: default, violet, grey, yellow, orange, red, green, blue, black, tracerYellow...</summary>
    [JsonPropertyName("backgroundColor")]
    public string BackgroundColor { get; set; } = "violet";

    [JsonPropertyName("weightKg")]
    public double WeightKg { get; set; } = 0.5;

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>Language code to name/short name/description. The first language is used for any language not listed.</summary>
    [JsonPropertyName("locales")]
    public Dictionary<string, ArtifactLocale> Locales { get; set; } = [];
}

public record ArtifactLocale
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("shortName")]
    public string ShortName { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";
}
