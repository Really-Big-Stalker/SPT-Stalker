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
