using RoslynWorker.Models;
using RoslynWorker.Models.Enums;

public sealed class PirGraph
{
    private readonly PirPackage pirPackage;

    private readonly Dictionary<string, PirNode> nodeLookup;
    private readonly Dictionary<string, List<PirRelationship>> outgoingRelationships;
    private readonly Dictionary<string, List<PirRelationship>> incomingRelationships;

    public PirGraph(PirPackage pirPackage)
    {
        this.pirPackage = pirPackage;

        nodeLookup = pirPackage.Nodes.ToDictionary(n => n.Id, n => n);

        outgoingRelationships = [];
        incomingRelationships = [];
        foreach (PirRelationship relationship in pirPackage.Relationships)
        {
            if (!outgoingRelationships.ContainsKey(relationship.SourceId))
            {
                outgoingRelationships[relationship.SourceId] = [];
            }

            if (!incomingRelationships.ContainsKey(relationship.TargetId))
            {
                incomingRelationships[relationship.TargetId] = [];
            }

            outgoingRelationships[relationship.SourceId].Add(relationship);
            incomingRelationships[relationship.TargetId].Add(relationship);
        }
    }

    public PirNode? GetNode(string id)
    {
        return nodeLookup.GetValueOrDefault(id);
    }

    public IEnumerable<PirRelationship> GetOutgoingRelationships(PirNode node)
    {
        if (!outgoingRelationships.TryGetValue(node.Id, out var relationships))
            return [];

        return relationships;
    }

    public IEnumerable<PirRelationship> GetIncomingRelationships(PirNode node)
    {
        if (!incomingRelationships.TryGetValue(node.Id, out var relationships))
            return [];

        return relationships;
    }

    public IEnumerable<PirNode> GetCallees(PirNode method)
    {
        return GetOutgoingRelationships(method)
            .Where(r => r.Type == PirRelationshipType.CALLS)
            .Select(r => nodeLookup[r.TargetId]);
    }

    public IEnumerable<PirNode> GetCallers(PirNode method)
    {
        return GetIncomingRelationships(method)
            .Where(r => r.Type == PirRelationshipType.CALLS)
            .Select(r => nodeLookup[r.SourceId]);
    }

    public IEnumerable<PirNode> GetReaders(PirNode node)
    {
        return GetIncomingRelationships(node)
            .Where(r => r.Type == PirRelationshipType.READS)
            .Select(r => nodeLookup[r.SourceId]);
    }

    public IEnumerable<PirNode> GetWriters(PirNode node)
    {
        return GetIncomingRelationships(node)
            .Where(r => r.Type == PirRelationshipType.WRITES)
            .Select(r => nodeLookup[r.SourceId]);
    }

    

    // GetOutgoingRelationships();
    // GetIncomingRelationships();

    // GetCallees();
    // GetCallers();

    // GetReaders();
    // GetWriters();
}
