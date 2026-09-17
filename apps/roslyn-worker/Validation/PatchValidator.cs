using Aegis.Sanitizer.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace RoslynWorker.Validation;

public sealed class PatchValidator
{
    public PatchValidationResult Validate(
        string originalSource,
        ReversePatch patch
    )
    {
        string patchedSource;

        try
        {
            patchedSource = ApplyPatch(
                originalSource,
                patch
            );
        }
        catch (Exception exception)
        {
            return new PatchValidationResult
            {
                Patch = patch,
                IsValid = false,
                HasSyntaxErrors = false,
                HasCompilationErrors = false,
                Diagnostics =
                [
                    $"Failed to apply patch in memory: {exception.Message}"
                ],
                ValidatedSource = null
            };
        }

        SyntaxTree syntaxTree =
            CSharpSyntaxTree.ParseText(
                patchedSource,
                path: patch.FilePath
            );

        List<Diagnostic> diagnostics =
            syntaxTree
                .GetDiagnostics()
                .Where(diagnostic =>
                    diagnostic.Severity == DiagnosticSeverity.Error
                )
                .ToList();

        bool hasSyntaxErrors =
            diagnostics.Count > 0;

        return new PatchValidationResult
        {
            Patch = patch,
            IsValid = !hasSyntaxErrors,
            HasSyntaxErrors = hasSyntaxErrors,
            HasCompilationErrors = false,
            Diagnostics = diagnostics
                .Select(diagnostic => diagnostic.ToString())
                .ToList(),
            ValidatedSource = patchedSource
        };
    }

    private static string ApplyPatch(
        string source,
        ReversePatch patch
    )
    {
        if (patch.Start < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(patch.Start)
            );
        }

        if (patch.Length < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(patch.Length)
            );
        }

        if (patch.Start + patch.Length > source.Length)
        {
            throw new ArgumentException(
                "Patch extends beyond the source."
            );
        }

        return source
            .Remove(
                patch.Start,
                patch.Length
            )
            .Insert(
                patch.Start,
                patch.ReplacementText
            );
    }
}