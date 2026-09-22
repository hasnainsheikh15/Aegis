namespace RoslynWorker.Models;

public sealed class PatchApplicationResult
{
    public required string FilePath { get; init; }

    public required bool Applied { get; init; }

    public required bool RequiresReview { get; init; }

    public required string Message { get; init; }

    public List<string> Diagnostics { get; init; } = [];
}