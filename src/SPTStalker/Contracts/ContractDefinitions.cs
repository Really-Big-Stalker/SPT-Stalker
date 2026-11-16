using System.Text.Json.Serialization;

namespace SPTStalker.Contracts;

/// <summary>data/contracts/contracts.json</summary>
public record ContractFile
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Repeatable group (Daily / Weekly) to how many "tickets" the Bunker gets when the game picks
    /// which trader hands out a task. Every vanilla trader has 1, so 2 = twice as likely as any one of them.
    /// </summary>
    [JsonPropertyName("traderWeight")]
    public Dictionary<string, int> TraderWeight { get; set; } = [];

    /// <summary>Item base classes the vanilla reward generator may pick reward items from.</summary>
    [JsonPropertyName("rewardBaseWhitelist")]
    public List<string> RewardBaseWhitelist { get; set; } = [];

    [JsonPropertyName("rewardCanBeWeapon")]
    public bool RewardCanBeWeapon { get; set; } = true;

    [JsonPropertyName("weaponRewardChancePercent")]
    public double WeaponRewardChancePercent { get; set; } = 15;

    [JsonPropertyName("hunts")]
    public HuntSettings Hunts { get; set; } = new();

    [JsonPropertyName("factions")]
    public List<FactionDefinition> Factions { get; set; } = [];

    [JsonPropertyName("modifiers")]
    public List<ModifierDefinition> Modifiers { get; set; } = [];

    [JsonPropertyName("artifactHunts")]
    public ArtifactHuntSettings ArtifactHunts { get; set; } = new();
}

public record HuntSettings
{
    /// <summary>Repeatable group to [min, max] kills before the faction's killMultiplier.</summary>
    [JsonPropertyName("kills")]
    public Dictionary<string, int[]> Kills { get; set; } = [];

    /// <summary>Repeatable group to the most extra conditions a contract can roll.</summary>
    [JsonPropertyName("maxModifiers")]
    public Dictionary<string, int> MaxModifiers { get; set; } = [];

    /// <summary>Chance a hunt is tied to one map instead of any map.</summary>
    [JsonPropertyName("mapLockChancePercent")]
    public double MapLockChancePercent { get; set; } = 50;
}

/// <summary>
/// An enemy group a hunt can target. Until the custom AI stage these map to existing bot types.
/// </summary>
public record FactionDefinition
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = "";

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("minLevel")]
    public int MinLevel { get; set; } = 1;

    /// <summary>Relative chance this faction is picked.</summary>
    [JsonPropertyName("weight")]
    public double Weight { get; set; } = 1;

    /// <summary>Game kill target: Savage, AnyPmc, Usec, Bear.</summary>
    [JsonPropertyName("target")]
    public string Target { get; set; } = "Savage";

    /// <summary>Bot roles (for Savage targets), e.g. assault, pmcBot, exUsec, sectantWarrior.</summary>
    [JsonPropertyName("savageRoles")]
    public List<string> SavageRoles { get; set; } = [];

    /// <summary>Maps (quest config location keys) where this faction spawns. Empty = any map.</summary>
    [JsonPropertyName("maps")]
    public List<string> Maps { get; set; } = [];

    /// <summary>Scales the kill count (rare enemies need fewer kills).</summary>
    [JsonPropertyName("killMultiplier")]
    public double KillMultiplier { get; set; } = 1;

    /// <summary>0..1, raises rewards.</summary>
    [JsonPropertyName("difficulty")]
    public double Difficulty { get; set; } = 0.3;

    /// <summary>Modifier keys this faction never rolls (e.g. "night" for enemies that only appear at night anyway).</summary>
    [JsonPropertyName("blockedModifiers")]
    public List<string> BlockedModifiers { get; set; } = [];

    [JsonPropertyName("texts")]
    public List<ContractText> Texts { get; set; } = [];
}

/// <summary>
/// An extra condition rolled onto a hunt. Types:
/// daytime (from, to), bodyPart (bodyParts), distance (compareMethod, value), caliber (calibers),
/// weaponClass (weaponBaseClasses), wearing (equipmentAnyOf), survive.
/// </summary>
public record ModifierDefinition
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = "";

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("chancePercent")]
    public double ChancePercent { get; set; } = 10;

    [JsonPropertyName("difficulty")]
    public double Difficulty { get; set; } = 0.2;

    [JsonPropertyName("minLevel")]
    public int MinLevel { get; set; } = 1;

    /// <summary>Modifier keys that can't be combined with this one.</summary>
    [JsonPropertyName("excludes")]
    public List<string> Excludes { get; set; } = [];

    [JsonPropertyName("from")]
    public int From { get; set; }

    [JsonPropertyName("to")]
    public int To { get; set; }

    [JsonPropertyName("bodyParts")]
    public List<string> BodyParts { get; set; } = [];

    [JsonPropertyName("compareMethod")]
    public string CompareMethod { get; set; } = ">=";

    [JsonPropertyName("value")]
    public double Value { get; set; }

    [JsonPropertyName("calibers")]
    public List<string> Calibers { get; set; } = [];

    [JsonPropertyName("weaponBaseClasses")]
    public List<string> WeaponBaseClasses { get; set; } = [];

    [JsonPropertyName("equipmentAnyOf")]
    public List<string> EquipmentAnyOf { get; set; } = [];
}

public record ArtifactHuntSettings
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>Repeatable group to [min, max] artifacts asked for in total.</summary>
    [JsonPropertyName("count")]
    public Dictionary<string, int[]> Count { get; set; } = [];

    /// <summary>Repeatable group to the most different artifact types in one contract.</summary>
    [JsonPropertyName("maxTypes")]
    public Dictionary<string, int> MaxTypes { get; set; } = [];

    /// <summary>[playerLevel, highestTier] pairs: from that level, artifacts up to that tier can be asked for.</summary>
    [JsonPropertyName("tierByLevel")]
    public List<int[]> TierByLevel { get; set; } = [];

    [JsonPropertyName("texts")]
    public List<ContractText> Texts { get; set; } = [];
}

/// <summary>Flavour text for one contract variant. Condition lines are written by the game itself.</summary>
public record ContractText
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("success")]
    public string Success { get; set; } = "";

    [JsonPropertyName("fail")]
    public string Fail { get; set; } = "";
}
