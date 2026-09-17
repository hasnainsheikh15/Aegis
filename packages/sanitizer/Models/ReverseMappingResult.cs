namespace Aegis.Sanitizer.Models;

public sealed class ReverseMappingResult
{
    public required SanitizedChange Change { get; init; }

    public required bool IsProtectedRegion { get; init; }

    public required string? ProtectedNodeId { get; init; }

    public required string? Reason { get; init; }
}