using System.Text.Json.Serialization;

namespace SPTStalker.Bots;

/// <summary>data/bots/stalkers.json: the roaming Loner stalker groups.</summary>
public record StalkerFile
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Game bot role the stalkers use. Must exist in the game's WildSpawnType list and have bot data;
    /// "arenaFighterEvent" (the Halloween Bloodhounds) is unused outside events and roams in groups.
    /// </summary>
    [JsonPropertyName("role")]
    public string Role { get; set; } = "arenaFighterEvent";

    [JsonPropertyName("names")]
    public List<string> Names { get; set; } = [];

    [JsonPropertyName("hostility")]
    public StalkerHostility Hostility { get; set; } = new();

    /// <summary>Map id to spawn settings; "default" covers maps not listed.</summary>
    [JsonPropertyName("spawns")]
    public Dictionary<string, StalkerSpawnSettings> Spawns { get; set; } = [];

    [JsonPropertyName("loadout")]
    public StalkerLoadout Loadout { get; set; } = new();
}

public record StalkerHostility
{
    /// <summary>How stalkers treat player PMCs / player scavs: Neutral, Warn, AlwaysEnemies, AlwaysFriends.</summary>
    [JsonPropertyName("towardsPlayerPmc")]
    public string TowardsPlayerPmc { get; set; } = "Neutral";

    [JsonPropertyName("towardsPlayerScav")]
    public string TowardsPlayerScav { get; set; } = "Neutral";

    [JsonPropertyName("enemies")]
    public List<string> Enemies { get; set; } = [];

    [JsonPropertyName("friends")]
    public List<string> Friends { get; set; } = [];
}

public record StalkerSpawnSettings
{
    /// <summary>Number of possible groups per raid; each rolls its own chance.</summary>
    [JsonPropertyName("groups")]
    public int Groups { get; set; } = 1;

    [JsonPropertyName("chancePercent")]
    public double ChancePercent { get; set; } = 50;

    /// <summary>[min, max] stalkers per group, leader included.</summary>
    [JsonPropertyName("groupSize")]
    public int[] GroupSize { get; set; } = [2, 4];

    /// <summary>Seconds into the raid; -1 = at raid start. Ignored when randomTime is true.</summary>
    [JsonPropertyName("time")]
    public double Time { get; set; } = -1;

    [JsonPropertyName("randomTime")]
    public bool RandomTime { get; set; } = true;
}

public record StalkerLoadout
{
    /// <summary>Bot types whose weapon attachments, armour plates and ammo pools the stalkers may use.</summary>
    [JsonPropertyName("sourceRoles")]
    public List<string> SourceRoles { get; set; } = ["assault", "pmcbot"];

    /// <summary>Equipment slot to item tpl to weight. Slots not listed keep the role's own pool.</summary>
    [JsonPropertyName("equipment")]
    public Dictionary<string, Dictionary<string, double>> Equipment { get; set; } = [];

    /// <summary>Equipment slot to chance (0-100) the slot is filled.</summary>
    [JsonPropertyName("equipmentChances")]
    public Dictionary<string, double> EquipmentChances { get; set; } = [];
}
