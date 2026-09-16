namespace Aegis.Sanitizer.Models;

public sealed class SourceSelection
{
    public required string FilePath { get; init; }

    public required int Start { get; init; }

    public required int Length { get; init; }

    public int End => Start + Length;
}