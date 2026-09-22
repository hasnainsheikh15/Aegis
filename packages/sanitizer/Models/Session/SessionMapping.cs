using Aegis.Pir.Enums;

namespace Aegis.Sanitizer.Models.Session;

public sealed class SessionMapping
{
    public required string NodeId { get; init; }

    public required PirNodeType NodeType { get; init; }

    public required string OriginalText { get; init; }

    public required string DummyText { get; init; }

    public required int OriginalStart { get; init; }

    public required int OriginalLength { get; init; }

    public required int SanitizedStart { get; init; }

    public required int SanitizedLength { get; init; }
}