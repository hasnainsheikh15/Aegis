namespace Aegis.Graph;
using Aegis.Pir;

public sealed class SensitivityFinding
{
    public SensitivityResult Source { get; init; } = null!;

    public PirNode Sink { get; init; } = null!;

    public List<PirNode> Path { get; init; } = [];
}