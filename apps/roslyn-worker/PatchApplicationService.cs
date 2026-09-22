using Aegis.Sanitizer;
using Aegis.Sanitizer.Models;
using RoslynWorker.Models;
using RoslynWorker.Validation;


namespace Aegis.Sanitizer;

public sealed class PatchApplicationService
{
    private readonly PatchApplier patchApplier;
    private readonly PatchValidator patchValidator;
    private readonly SourceHasher sourceHasher;

    public PatchApplicationService(
        PatchApplier patchApplier,
        PatchValidator patchValidator,
        SourceHasher sourceHasher
    )
    {
        this.patchApplier = patchApplier;
        this.patchValidator = patchValidator;
        this.sourceHasher = sourceHasher;
    }

    public PatchApplicationResult Apply(
        string filePath,
        string expectedOriginalHash,
        IEnumerable<ReversePatch> patches
    )
    {
        if (!File.Exists(filePath))
        {
            return new PatchApplicationResult
            {
                FilePath = filePath,
                Applied = false,
                RequiresReview = true,
                Message = "Original source file does not exist."
            };
        }

        string originalSource =
            File.ReadAllText(filePath);

        string currentHash =
            sourceHasher.Compute(originalSource);

        if (
            !string.Equals(
                currentHash,
                expectedOriginalHash,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return new PatchApplicationResult
            {
                FilePath = filePath,
                Applied = false,
                RequiresReview = true,
                Message =
                    "Original source changed after the session was created."
            };
        }

        List<ReversePatch> patchList =
            patches.ToList();

        if (patchList.Count == 0)
        {
            return new PatchApplicationResult
            {
                FilePath = filePath,
                Applied = false,
                RequiresReview = false,
                Message = "No changes to apply."
            };
        }

        if (
            patchList.Any(
                patch => patch.RequiresReview
            )
        )
        {
            return new PatchApplicationResult
            {
                FilePath = filePath,
                Applied = false,
                RequiresReview = true,
                Message =
                    "One or more patches require review."
            };
        }

        string patchedSource;

        try
        {
            patchedSource =
                patchApplier.Apply(
                    originalSource,
                    patchList
                );
        }
        catch (Exception exception)
        {
            return new PatchApplicationResult
            {
                FilePath = filePath,
                Applied = false,
                RequiresReview = true,
                Message =
                    $"Failed to apply patches: {exception.Message}"
            };
        }

        PatchValidationResult validation =
            patchValidator.ValidateSource(
                patchedSource,
                filePath
            );

        if (!validation.IsValid)
        {
            return new PatchApplicationResult
            {
                FilePath = filePath,
                Applied = false,
                RequiresReview = true,
                Message =
                    "Patched source failed syntax validation.",
                Diagnostics =
                    validation.Diagnostics
            };
        }

        File.WriteAllText(
            filePath,
            patchedSource
        );

        return new PatchApplicationResult
        {
            FilePath = filePath,
            Applied = true,
            RequiresReview = false,
            Message = "Patches applied successfully.",
            Diagnostics = []
        };
    }
}