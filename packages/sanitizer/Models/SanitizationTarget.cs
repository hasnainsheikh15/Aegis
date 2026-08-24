using Aegis.Pir.Enums;

namespace Aegis.Sanitizer.Models;

public sealed class SanitizationTarget
{
    public required string NodeId { get; init; }

    public required PirNodeType NodeType { get; init; }

    public required string FilePath { get; init; }

    public required int Start { get; init; }

    public required int Length { get; init; }

    public required string OriginalText { get; init; }
}