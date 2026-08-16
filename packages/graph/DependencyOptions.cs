using RoslynWorker.Models.Enums;

public sealed class DependencyOptions
{
    public HashSet<PirRelationshipType> RelationshipTypes { get; init; } = [];
}