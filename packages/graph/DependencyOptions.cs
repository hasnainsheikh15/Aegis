namespace Aegis.Graph;
using Aegis.Pir.Enums;

public sealed class DependencyOptions
{
    public HashSet<PirRelationshipType> RelationshipTypes { get; init; } = [];
}