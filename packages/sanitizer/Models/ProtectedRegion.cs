namespace Aegis.Sanitizer.Models;

public sealed class ProtectedRegion
{
    public required int Start { get; init; }

    public required int Length { get; init; }

    public required string DummyText { get; init; }

    public required string OriginalText { get; init; }

    public required string NodeId { get; init; }
}