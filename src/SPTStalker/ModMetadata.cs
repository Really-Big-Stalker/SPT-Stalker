using SPTarkov.Server.Core.Models.Spt.Mod;

namespace SPTStalker;

/// <summary>
/// Describes the mod to the SPT server. Exactly one IModMetadata per mod assembly.
/// </summary>
public record ModMetadata : IModMetadata
{
    public string ModGuid { get; init; } = "com.sptstalker.zone";
    public string Name { get; init; } = "SPT-Stalker";
    public string Author { get; init; } = "SPT-Stalker";
    public List<string>? Contributors { get; init; }
    public SemanticVersioning.Version Version { get; init; } = new("0.1.0");

    // Any SPT 4.1.x server (4.1.0 up to, but not including, 4.2.0)
    public SemanticVersioning.Range SptVersion { get; init; } = new("~4.1.0");

    public List<string>? Incompatibilities { get; init; }
    public Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; }
    public string? Url { get; init; }
    public string License { get; init; } = "MIT";
    public bool HasPrepatcher { get; init; } = false;
}
