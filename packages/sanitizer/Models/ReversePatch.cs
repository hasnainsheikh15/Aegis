namespace Aegis.Sanitizer.Models;

public sealed class ReversePatch
{
    public required string FilePath { get; init; }

    public required int Start { get; init; }

    public required int Length { get; init; }

    public required string OriginalText { get; init; }

    public required string ReplacementText { get; init; }

    public required bool RequiresReview { get; init; }

    public required string Reason { get; init; }
}