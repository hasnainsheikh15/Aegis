using RoslynWorker.Models.Enums;
using RoslynWorker.Models;

public sealed class DependencyPathStep
{
    public PirNode Source { get; init; } = null!;

    public PirRelationshipType RelationshipType { get; init; }

    public PirNode Target { get; init; } = null!;
}
