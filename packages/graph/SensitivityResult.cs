using RoslynWorker.Models;
using RoslynWorker.Models.Enums;

public sealed class SensitivityResult
{
    public PirNode Node { get; init; } = null!;

    public SensitivityLevel Level { get; init; }

    public List<string> Reasons { get; init; } = [];
}
