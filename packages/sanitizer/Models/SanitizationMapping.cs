namespace Aegis.Sanitizer.Models;

public sealed class SanitizationMapping
{
    public required string NodeId { get; init; }

    public required string OriginalText { get; init; }

    public required string DummyText { get; init; }

    public required string FilePath { get; init; }

    public required int OriginalStart { get; init; }

    public required int OriginalLength { get; init; }
}