using Aegis.Pir;
namespace Aegis.Graph;
public sealed class SinkAnalyzer
{
    private readonly HashSet<string> sinkNames;

    public SinkAnalyzer(IEnumerable<string> sinkNames)
    {
        this.sinkNames = new HashSet<string>(
            sinkNames,
            StringComparer.OrdinalIgnoreCase
        );
    }

    public bool IsSink(PirNode node)
    {
        return sinkNames.Contains(node.Name);
    }
}