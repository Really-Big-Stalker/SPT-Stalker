using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;

namespace SPTStalker.Artifacts;

/// <summary>Holds the artifacts that were successfully added, so other systems (contracts) can use them.</summary>
[Injectable(InjectionType.Singleton)]
public class ArtifactRegistry
{
    private readonly List<RegisteredArtifact> _artifacts = [];

    public IReadOnlyList<RegisteredArtifact> All => _artifacts;

    public void Add(RegisteredArtifact artifact) => _artifacts.Add(artifact);
}

public record RegisteredArtifact(MongoId Tpl, ArtifactDefinition Definition);
