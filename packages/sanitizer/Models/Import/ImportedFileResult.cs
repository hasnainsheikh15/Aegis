using Aegis.Sanitizer;

namespace Aegis.Sanitizer.Models.Import;

public sealed class ImportedFileResult
{
    public required string OriginalFilePath { get; init; }

    public required List<ReverseMappedChange> Changes { get; init; }

    public required List<ReversePatch> Patches { get; init; }

    public required List<string> ReviewReasons { get; init; }

    public bool RequiresReview =>
        ReviewReasons.Count > 0
        || Patches.Any(patch => patch.RequiresReview);
}