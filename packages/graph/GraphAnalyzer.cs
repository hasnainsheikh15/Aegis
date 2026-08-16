using RoslynWorker.Models;
using RoslynWorker.Models.Enums;

public sealed class GraphAnalyzer
{
    private readonly PirGraph graph;

    public GraphAnalyzer(PirGraph graph)
    {
        this.graph = graph;
    }

    public IEnumerable<PirNode> GetReachableMethods(PirNode start)
    {
        HashSet<string> visited = [start.Id];

        List<PirNode> result = [];

        DFS(start, visited, result, graph.GetCallees);

        return result;
    }

    public IEnumerable<PirNode> GetImpactedMethods(PirNode start)
    {
        HashSet<string> visited = [start.Id];

        List<PirNode> result = [];

        DFS(start, visited, result, graph.GetCallers);

        return result;
    }

    private void DFS(
        PirNode node,
        HashSet<string> visited,
        List<PirNode> result,
        Func<PirNode, IEnumerable<PirNode>> getNext
    )
    {
        foreach (PirNode next in getNext(node))
        {
            if (!visited.Add(next.Id))
                continue;

            result.Add(next);

            DFS(next, visited, result, getNext);
        }
    }

    public IEnumerable<PirNode> GetDependencies(PirNode start, DependencyOptions options)
    {
        HashSet<string> visited = [start.Id];

        List<PirNode> result = [];

        DFS(start, visited, result, node => GetDependencyNodes(node, options));

        return result;
    }

    private IEnumerable<PirNode> GetDependencyNodes(PirNode node, DependencyOptions options)
    {
        return graph
            .GetOutgoingRelationships(node)
            .Where(r => options.RelationshipTypes.Contains(r.Type))
            .Select(r => graph.GetNode(r.TargetId))
            .Where(n => n is not null)!;
    }

    private IEnumerable<DependencyPathStep> GetDependencySteps(
        PirNode node,
        DependencyOptions options
    )
    {
        return graph
            .GetOutgoingRelationships(node)
            .Where(r => options.RelationshipTypes.Contains(r.Type))
            .Select(r =>
            {
                PirNode? target = graph.GetNode(r.TargetId);

                if (target is null)
                    return null;

                return new DependencyPathStep
                {
                    Source = node,
                    RelationshipType = r.Type,
                    Target = target,
                };
            })
            .Where(step => step is not null)!;
    }

    public IEnumerable<List<DependencyPathStep>> GetDependencyPaths(PirNode start , DependencyOptions options) {
        HashSet<string> visited = [start.Id];

        List<List<DependencyPathStep>> paths = [];

        DFSPaths(start,options,visited,[],paths);

        return paths;
    }

    private void DFSPaths(
    PirNode node,
    DependencyOptions options,
    HashSet<string> visited,
    List<DependencyPathStep> currentPath,
    List<List<DependencyPathStep>> paths)
{
    foreach (DependencyPathStep step in GetDependencySteps(node, options))
    {
        if (!visited.Add(step.Target.Id))
        {
            continue;
        }

        currentPath.Add(step);

        paths.Add(new List<DependencyPathStep>(currentPath));

        DFSPaths(
            step.Target,
            options,
            visited,
            currentPath,
            paths
        );

        currentPath.RemoveAt(currentPath.Count - 1);
        visited.Remove(step.Target.Id);
    }
}
}
