namespace Aegis.Sanitizer.Models.Session;

public sealed class SessionFile
{
    public required string OriginalFilePath { get; init; }

    public required string SanitizedFilePath { get; init; }

    public required string BaselineSanitizedFilePath { get; init; }

    public required string OriginalSourceHash { get; init; }

    public required string SanitizedSourceHash { get; init; }

    public required List<SessionMapping> Mappings { get; init; }
}