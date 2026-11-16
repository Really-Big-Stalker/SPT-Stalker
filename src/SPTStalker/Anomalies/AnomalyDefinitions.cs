using System.Text.Json.Serialization;

namespace SPTStalker.Anomalies;

/// <summary>data/anomalies/anomalies.json</summary>
public record AnomalyFile
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>Fields keep at least this far (metres) from player spawn points.</summary>
    [JsonPropertyName("minDistanceFromPlayerSpawn")]
    public double MinDistanceFromPlayerSpawn { get; set; } = 40;

    /// <summary>Fields keep at least this far (metres) from each other.</summary>
    [JsonPropertyName("minDistanceBetweenFields")]
    public double MinDistanceBetweenFields { get; set; } = 60;

    /// <summary>Map id (bigmap, woods, ...) to settings; "default" is used for maps not listed.</summary>
    [JsonPropertyName("maps")]
    public Dictionary<string, MapAnomalySettings> Maps { get; set; } = [];

    [JsonPropertyName("types")]
    public List<AnomalyType> Types { get; set; } = [];

    [JsonPropertyName("radiation")]
    public RadiationSettings Radiation { get; set; } = new();

    [JsonPropertyName("armor")]
    public ArmorSettings Armor { get; set; } = new();
}

/// <summary>How worn armour softens physical anomaly hits (springboard slams, electro discharges...).</summary>
public record ArmorSettings
{
    /// <summary>Reduction per armour class (class 4 x 0.1 = 40%).</summary>
    [JsonPropertyName("reductionPerClass")]
    public float ReductionPerClass { get; set; } = 0.1f;

    [JsonPropertyName("maxReduction")]
    public float MaxReduction { get; set; } = 0.6f;

    /// <summary>Slots whose highest armour class protects the body (plates and soft inserts inside count).</summary>
    [JsonPropertyName("bodySlots")]
    public List<string> BodySlots { get; set; } = ["ArmorVest", "TacticalVest"];

    /// <summary>Slots whose highest armour class protects the head.</summary>
    [JsonPropertyName("headSlots")]
    public List<string> HeadSlots { get; set; } = ["Headwear"];
}

/// <summary>The player's radiation dose: how it builds up, what it does, and what protects against it.</summary>
public record RadiationSettings
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>Dose shown as "current / max" in the health screen's radiation meter.</summary>
    [JsonPropertyName("maxDose")]
    public float MaxDose { get; set; } = 100;

    /// <summary>Dose the body clears by itself per second.</summary>
    [JsonPropertyName("decayPerSecond")]
    public float DecayPerSecond { get; set; } = 0.01f;

    /// <summary>Radiation per second given off by each carried artifact, by tier 1-4.</summary>
    [JsonPropertyName("artifactRadiationByTier")]
    public float[] ArtifactRadiationByTier { get; set; } = [0.02f, 0.04f, 0.08f, 0.15f];

    /// <summary>Equipment slots whose contents don't irradiate you (the secure container counts as lead-lined).</summary>
    [JsonPropertyName("shieldedSlots")]
    public List<string> ShieldedSlots { get; set; } = ["SecuredContainer"];

    /// <summary>Sub-slots (inside other items) that are shielded too, e.g. the pockets' special slots.</summary>
    [JsonPropertyName("shieldedSubSlots")]
    public List<string> ShieldedSubSlots { get; set; } = ["SpecialSlot1", "SpecialSlot2", "SpecialSlot3"];

    /// <summary>Allow artifacts to be put in the pockets' special slots.</summary>
    [JsonPropertyName("artifactsFitSpecialSlots")]
    public bool ArtifactsFitSpecialSlots { get; set; } = true;

    [JsonPropertyName("gasMasks")]
    public List<GasMaskDefinition> GasMasks { get; set; } = [];

    [JsonPropertyName("cures")]
    public List<RadiationCure> Cures { get; set; } = [];

    /// <summary>What happens at each dose level (highest reached applies).</summary>
    [JsonPropertyName("stages")]
    public List<RadiationStage> Stages { get; set; } = [];
}

public record GasMaskDefinition
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("tpl")]
    public string Tpl { get; set; } = "";

    /// <summary>0..1 share of airborne harm blocked.</summary>
    [JsonPropertyName("protection")]
    public float Protection { get; set; }
}

public record RadiationCure
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("tpl")]
    public string Tpl { get; set; } = "";

    /// <summary>Dose removed when used/drunk.</summary>
    [JsonPropertyName("doseReduction")]
    public float DoseReduction { get; set; }
}

public record RadiationStage
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    /// <summary>Stage applies from this dose upwards.</summary>
    [JsonPropertyName("dose")]
    public float Dose { get; set; }

    /// <summary>Radiation damage per second while at this stage.</summary>
    [JsonPropertyName("damagePerSecond")]
    public float DamagePerSecond { get; set; }

    [JsonPropertyName("bodyParts")]
    public List<string> BodyParts { get; set; } = ["Chest", "Stomach"];

    /// <summary>Seconds between bouts of nausea/dizziness (contusion); 0 = none.</summary>
    [JsonPropertyName("sicknessEverySeconds")]
    public float SicknessEverySeconds { get; set; }

    /// <summary>Also start the game's own "radiation exposure" status effect when entering this stage.</summary>
    [JsonPropertyName("gameRadiationEffect")]
    public bool GameRadiationEffect { get; set; }
}

public record MapAnomalySettings
{
    /// <summary>[min, max] anomaly fields per raid.</summary>
    [JsonPropertyName("fields")]
    public int[] Fields { get; set; } = [3, 6];

    /// <summary>[min, max] anomalies in one field.</summary>
    [JsonPropertyName("anomaliesPerField")]
    public int[] AnomaliesPerField { get; set; } = [2, 5];

    /// <summary>How far (metres) anomalies of a field spread from its centre.</summary>
    [JsonPropertyName("fieldRadius")]
    public double FieldRadius { get; set; } = 8;

    /// <summary>Chance a field holds an artifact of its anomaly family at its centre.</summary>
    [JsonPropertyName("artifactChancePercent")]
    public double ArtifactChancePercent { get; set; } = 40;

    /// <summary>Anomaly type key to relative chance on this map (types not listed use their own weight).</summary>
    [JsonPropertyName("typeWeights")]
    public Dictionary<string, double> TypeWeights { get; set; } = [];
}

/// <summary>
/// One kind of anomaly. Everything except key/weight/family/radius is passed to the client plugin,
/// which applies the effect while the player is inside.
/// </summary>
public record AnomalyType
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = "";

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("weight")]
    public double Weight { get; set; } = 1;

    /// <summary>Artifact family that forms here (matches artifacts.json "anomaly"); empty = no artifacts.</summary>
    [JsonPropertyName("family")]
    public string Family { get; set; } = "";

    /// <summary>[min, max] radius in metres of each anomaly of this type.</summary>
    [JsonPropertyName("radius")]
    public double[] Radius { get; set; } = [1.5, 2.5];

    [JsonPropertyName("effect")]
    public AnomalyEffect Effect { get; set; } = new();
}

public record AnomalyEffect
{
    /// <summary>EFT damage type: Flame, Poison, RadExposure, Blunt, Environment...</summary>
    [JsonPropertyName("damageType")]
    public string DamageType { get; set; } = "Environment";

    /// <summary>Damage per second while inside (spread over bodyParts).</summary>
    [JsonPropertyName("damagePerSecond")]
    public float DamagePerSecond { get; set; }

    /// <summary>One-off damage when a burst fires (on entry, then every burstInterval seconds while inside).</summary>
    [JsonPropertyName("burstDamage")]
    public float BurstDamage { get; set; }

    [JsonPropertyName("burstInterval")]
    public float BurstInterval { get; set; } = 3;

    /// <summary>Body parts hit: Head, Chest, Stomach, LeftArm, RightArm, LeftLeg, RightLeg.</summary>
    [JsonPropertyName("bodyParts")]
    public List<string> BodyParts { get; set; } = ["LeftLeg", "RightLeg"];

    [JsonPropertyName("stunSeconds")]
    public float StunSeconds { get; set; }

    [JsonPropertyName("contusionSeconds")]
    public float ContusionSeconds { get; set; }

    [JsonPropertyName("fractureChancePercent")]
    public float FractureChancePercent { get; set; }

    [JsonPropertyName("bleedChancePercent")]
    public float BleedChancePercent { get; set; }

    [JsonPropertyName("intoxicationChancePercent")]
    public float IntoxicationChancePercent { get; set; }

    /// <summary>Light colour (0-1 RGB), intensity and flicker speed for the in-raid look.</summary>
    [JsonPropertyName("lightColor")]
    public float[] LightColor { get; set; } = [1, 1, 1];

    [JsonPropertyName("lightIntensity")]
    public float LightIntensity { get; set; } = 2;

    [JsonPropertyName("flicker")]
    public float Flicker { get; set; } = 1;

    /// <summary>Hum pitch (Hz) of the anomaly's sound; 0 = silent.</summary>
    [JsonPropertyName("humPitch")]
    public float HumPitch { get; set; } = 80;

    /// <summary>Radiation dose per second while inside the anomaly.</summary>
    [JsonPropertyName("radiationPerSecond")]
    public float RadiationPerSecond { get; set; }

    /// <summary>Radiation dose per second in the area around it (out to radius x auraRadiusMultiplier).</summary>
    [JsonPropertyName("auraRadiationPerSecond")]
    public float AuraRadiationPerSecond { get; set; }

    [JsonPropertyName("auraRadiusMultiplier")]
    public float AuraRadiusMultiplier { get; set; } = 3;

    /// <summary>
    /// 0..1: how much of this anomaly's airborne harm (damage over time, radiation, intoxication) a gas mask can
    /// block. Multiplied by the mask's own protection. Bursts (slams, discharges) are never blocked.
    /// </summary>
    [JsonPropertyName("maskProtection")]
    public float MaskProtection { get; set; }

    /// <summary>
    /// 0..1: how much of this anomaly's physical harm (bursts: damage, contusion, stun, fracture, bleeding)
    /// armour can absorb. Multiplied by the armour's own reduction (see "armor" settings).
    /// </summary>
    [JsonPropertyName("armorProtection")]
    public float ArmorProtection { get; set; }
}

// ------------------------------------------------------------- layout sent to the client

/// <summary>What the client receives from /sptstalker/anomalies for the current raid.</summary>
public record AnomalyLayout
{
    [JsonPropertyName("location")]
    public string Location { get; set; } = "";

    [JsonPropertyName("fields")]
    public List<AnomalyFieldDto> Fields { get; set; } = [];

    [JsonPropertyName("types")]
    public List<AnomalyTypeDto> Types { get; set; } = [];

    [JsonPropertyName("radiation")]
    public RadiationSettings Radiation { get; set; } = new();

    /// <summary>Artifact templates and how much radiation each gives off when carried.</summary>
    [JsonPropertyName("artifacts")]
    public List<ArtifactRadiationDto> Artifacts { get; set; } = [];

    [JsonPropertyName("armor")]
    public ArmorSettings Armor { get; set; } = new();

    /// <summary>Armour class of every item that has one (vests, rigs, plates, inserts, helmets).</summary>
    [JsonPropertyName("armorClasses")]
    public List<ArmorClassDto> ArmorClasses { get; set; } = [];
}

public record ArmorClassDto
{
    [JsonPropertyName("tpl")]
    public string Tpl { get; set; } = "";

    [JsonPropertyName("armorClass")]
    public int ArmorClass { get; set; }
}

public record ArtifactRadiationDto
{
    [JsonPropertyName("tpl")]
    public string Tpl { get; set; } = "";

    [JsonPropertyName("radiationPerSecond")]
    public float RadiationPerSecond { get; set; }
}

public record AnomalyFieldDto
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("center")]
    public Vec3 Center { get; set; } = new();

    /// <summary>Artifact key placed at the centre, if any (informational).</summary>
    [JsonPropertyName("artifact")]
    public string Artifact { get; set; } = "";

    [JsonPropertyName("anomalies")]
    public List<AnomalyDto> Anomalies { get; set; } = [];
}

public record AnomalyDto
{
    [JsonPropertyName("position")]
    public Vec3 Position { get; set; } = new();

    [JsonPropertyName("radius")]
    public float Radius { get; set; }
}

public record AnomalyTypeDto
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = "";

    [JsonPropertyName("effect")]
    public AnomalyEffect Effect { get; set; } = new();
}

/// <summary>Lower-case x/y/z so Unity's JsonUtility can read it straight into a Vector3.</summary>
public record Vec3
{
    [JsonPropertyName("x")]
    public float X { get; set; }

    [JsonPropertyName("y")]
    public float Y { get; set; }

    [JsonPropertyName("z")]
    public float Z { get; set; }
}
