using Aegis.Sanitizer.Models;

namespace Aegis.Sanitizer.Models;

public sealed class ReverseMappedChange
{
    public required SanitizedChange SourceChange { get; init; }

    public required bool IsSafe { get; init; }

    public required string? RealText { get; init; }

    public required string? ReplacementText { get; init; }

    public required string? Reason { get; init; }

    public required List<ProtectedRegion> ProtectedRegions { get; init; }
}