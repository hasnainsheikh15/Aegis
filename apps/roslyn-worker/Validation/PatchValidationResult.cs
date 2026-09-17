using Aegis.Sanitizer.Models;

namespace RoslynWorker.Validation;

public sealed class PatchValidationResult
{
    public required ReversePatch Patch { get; init; }

    public required bool IsValid { get; init; }

    public required bool HasSyntaxErrors { get; init; }

    public required bool HasCompilationErrors { get; init; }

    public required List<string> Diagnostics { get; init; }

    public required string? ValidatedSource { get; init; }
}