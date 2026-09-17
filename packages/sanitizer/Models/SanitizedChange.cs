namespace Aegis.Sanitizer.Models;

public sealed class SanitizedChange
{
    public required string FilePath { get; init; }

    public required int Start { get; init; }

    public required int OriginalLength { get; init; }

    public required string OriginalText { get; init; }

    public required int NewLength { get; init; }

    public required string NewText { get; init; }
}