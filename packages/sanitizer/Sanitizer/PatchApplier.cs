using Aegis.Sanitizer.Models;

namespace Aegis.Sanitizer;

public sealed class PatchApplier
{
    public string Apply(
        string originalSource,
        IEnumerable<ReversePatch> patches
    )
    {
        ArgumentNullException.ThrowIfNull(originalSource);
        ArgumentNullException.ThrowIfNull(patches);

        List<ReversePatch> patchList =
            patches.ToList();

        /*
         * Safety rule:
         *
         * Never apply anything if even one patch
         * requires human review.
         */
        if (patchList.Any(
            patch => patch.RequiresReview
        ))
        {
            throw new InvalidOperationException(
                "Cannot apply patches because one or more "
                + "patches require review."
            );
        }

        /*
         * Apply patches from right to left.
         *
         * This prevents changes earlier in the source
         * from shifting the coordinates of patches
         * that occur later.
         */
        List<ReversePatch> orderedPatches =
            patchList
                .OrderByDescending(
                    patch => patch.Start
                )
                .ToList();

        string result =
            originalSource;

        foreach (ReversePatch patch in orderedPatches)
        {
            if (
                patch.Start < 0
                || patch.Start > result.Length
                || patch.Start + patch.Length > result.Length
            )
            {
                throw new InvalidOperationException(
                    "Patch coordinates are outside "
                    + "the source range."
                );
            }

            string actualText =
                result.Substring(
                    patch.Start,
                    patch.Length
                );

            /*
             * Strong protection against applying a patch
             * to source that no longer matches what the
             * patch was generated against.
             */
            if (actualText != patch.OriginalText)
            {
                throw new InvalidOperationException(
                    "The source text at the patch location "
                    + "does not match the expected original text."
                );
            }

            result =
                result
                    .Remove(
                        patch.Start,
                        patch.Length
                    )
                    .Insert(
                        patch.Start,
                        patch.ReplacementText
                    );
        }

        return result;
    }
}