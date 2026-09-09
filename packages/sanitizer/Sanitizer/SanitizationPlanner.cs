using Aegis.Pir;
using Aegis.Sanitizer.Models;

namespace Aegis.Sanitizer;

public sealed class SanitizationPlanner
{
    public List<SanitizationTarget> Plan(
        IEnumerable<PirNode> sensitiveNodes,
        Func<PirNode, SanitizationTarget?> targetResolver
    )
    {
        List<SanitizationTarget> targets = [];

        foreach (PirNode node in sensitiveNodes)
        {
            SanitizationTarget? target = targetResolver(node);

            if (target is null)
            {
                continue;
            }

            targets.Add(target);
        }

        return targets;
    }
}