namespace Aegis.Sanitizer.Models.Import;

public sealed class ImportResult
{
    public required string SessionId { get; init; }

    public required List<ImportedFileResult> Files { get; init; }

    public bool RequiresReview =>
        Files.Any(file => file.RequiresReview);
}