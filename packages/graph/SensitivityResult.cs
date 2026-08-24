namespace Aegis.Graph;
using Aegis.Pir;
using Aegis.Pir.Enums;

public sealed class SensitivityResult
{
    public PirNode Node { get; init; } = null!;

    public SensitivityLevel Level { get; init; }

    public List<string> Reasons { get; init; } = [];
}
