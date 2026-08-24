using Aegis.Pir;
using Aegis.Pir.Enums;

namespace Aegis.Graph;

public sealed class SensitivityPropagator
{
    private readonly PirGraph graph;

    private readonly PirPackage pirPackage;

    public SensitivityPropagator(PirGraph graph, PirPackage pirPackage)
    {
        this.graph = graph;
        this.pirPackage = pirPackage;
    }

    public Dictionary<string, SensitivityLevel> Propagate(IEnumerable<SensitivityResult> findings)
    {
        Dictionary<string, SensitivityLevel> sensitivity = [];

        foreach (SensitivityResult finding in findings)
        {
            sensitivity[finding.Node.Id] = finding.Level;
        }

        bool changed = true;

        while (changed)
        {
            changed = false;

            foreach (PirNode node in pirPackage.Nodes)
            {
                if (!sensitivity.TryGetValue(node.Id, out SensitivityLevel sourceLevel))
                {
                    continue;
                }

                foreach (PirNode target in GetFlowTargets(node))
                {
                    if (
                        !sensitivity.TryGetValue(target.Id, out SensitivityLevel targetLevel)
                        || sourceLevel > targetLevel
                    )
                    {
                        sensitivity[target.Id] = sourceLevel;
                        changed = true;
                    }
                }
            }
        }

        return sensitivity;
    }

    private IEnumerable<PirNode> GetFlowTargets(PirNode node)
    {
        return graph
            .GetOutgoingRelationships(node)
            .Where(r => r.Type == PirRelationshipType.FLOWS_TO)
            .Select(r => graph.GetNode(r.TargetId))
            .Where(n => n is not null)!;
    }
}
