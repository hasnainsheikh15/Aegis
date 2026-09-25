namespace Aegis.Sanitizer.Models.Session;

public sealed class AegisSession
{
    public int Version { get; init; } = 1;

    public required string SessionId { get; init; }

    public required List<SessionFile> Files { get; init; }

    public SessionStatus Status { get; set; } = SessionStatus.Ready;
}