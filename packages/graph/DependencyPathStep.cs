namespace Aegis.Graph;
using Aegis.Pir.Enums;
using Aegis.Pir;

public sealed class DependencyPathStep
{
    public PirNode Source { get; init; } = null!;

    public PirRelationshipType RelationshipType { get; init; }

    public PirNode Target { get; init; } = null!;
}
