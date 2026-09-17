using Aegis.Sanitizer.Models;

namespace Aegis.Sanitizer;

public sealed class ReverseMapper
{
    public List<ReverseMappedChange> Analyze(
        IEnumerable<SanitizedChange> changes,
        IEnumerable<SanitizationMapping> mappings
    )
    {
        List<SanitizationMapping> mappingList = mappings
            .OrderBy(mapping => mapping.SanitizedStart)
            .ToList();

        List<ReverseMappedChange> results = [];

        foreach (SanitizedChange change in changes)
        {
            List<ProtectedRegion> protectedRegions = [];

            foreach (SanitizationMapping mapping in mappingList)
            {
                ProtectedRegion? region =
                    FindProtectedRegion(change, mapping);

                if (region is not null)
                {
                    protectedRegions.Add(region);
                }
            }

            bool isProtected =
                protectedRegions.Count > 0;

            results.Add(
                new ReverseMappedChange
                {
                    SourceChange = change,
                    IsSafe = !isProtected,
                    RealText = null,
                    ReplacementText = null,
                    Reason = isProtected
                        ? "Change contains a protected sanitized value."
                        : null,
                    ProtectedRegions = protectedRegions,
                }
            );
        }

        return results;
    }

    public ReversePatch? CreatePatch(
        ReverseMappedChange mappedChange,
        IEnumerable<SanitizationMapping> mappings,
        string originalSource
    )
    {
        SanitizedChange change =
            mappedChange.SourceChange;

        List<SanitizationMapping> mappingList =
            mappings
                .OrderBy(mapping => mapping.SanitizedStart)
                .ToList();

        /*
         * The change.Start is a position in the sanitized source.
         * Translate only the start position.
         *
         * We do NOT translate the end independently because the
         * changed range may contain one or more sanitized regions
         * whose lengths differ from their real values.
         */
        int realStart =
            TranslateSanitizedPositionToOriginal(
                change.Start,
                mappingList
            );

        /*
         * Reconstruct the exact text that currently exists in the
         * real source for this changed region.
         *
         * For example:
         *
         * Sanitized:
         *     private string password = "DUMMY_PASSWORD";
         *
         * Real:
         *     private string password = "abc123";
         */
        string realOriginalText =
            ReconstructOriginalText(
                change.OriginalText,
                mappingList
            );

        int realLength =
            realOriginalText.Length;

        /*
         * Make sure the calculated range actually exists in the
         * original source.
         */
        if (
            realStart < 0
            || realStart > originalSource.Length
            || realStart + realLength > originalSource.Length
        )
        {
            return new ReversePatch
            {
                FilePath = change.FilePath,
                Start = Math.Max(0, realStart),
                Length = 0,
                OriginalText = "",
                ReplacementText = "",
                RequiresReview = true,
                Reason =
                    "The reverse-mapped source range is invalid."
            };
        }

        /*
         * Use the actual source text at the calculated location.
         *
         * This is our strongest consistency check:
         *
         * ReversePatch.OriginalText must exactly match the real
         * source at Start/Length.
         */
        string sourceOriginalText =
            originalSource.Substring(
                realStart,
                realLength
            );

        if (sourceOriginalText != realOriginalText)
        {
            return new ReversePatch
            {
                FilePath = change.FilePath,
                Start = realStart,
                Length = realLength,
                OriginalText = sourceOriginalText,
                ReplacementText = sourceOriginalText,
                RequiresReview = true,
                Reason =
                    "The reverse-mapped text does not match the "
                    + "original source at the calculated location."
            };
        }

        if (!mappedChange.IsSafe)
        {
            return CreateProtectedPatch(
                mappedChange,
                realStart,
                realLength,
                sourceOriginalText
            );
        }

        return new ReversePatch
        {
            FilePath = change.FilePath,
            Start = realStart,
            Length = realLength,
            OriginalText = sourceOriginalText,
            ReplacementText = change.NewText,
            RequiresReview = false,
            Reason =
                "Change does not overlap a protected region."
        };
    }

    private static ReversePatch? CreateProtectedPatch(
        ReverseMappedChange mappedChange,
        int realStart,
        int realLength,
        string realOriginalText
    )
    {
        SanitizedChange change =
            mappedChange.SourceChange;

        if (mappedChange.ProtectedRegions.Count != 1)
        {
            return new ReversePatch
            {
                FilePath = change.FilePath,
                Start = realStart,
                Length = realLength,
                OriginalText = realOriginalText,
                ReplacementText = realOriginalText,
                RequiresReview = true,
                Reason =
                    "Change contains multiple protected regions and "
                    + "cannot be safely reconstructed automatically."
            };
        }

        ProtectedRegion protectedRegion =
            mappedChange.ProtectedRegions[0];

        /*
         * The LLM must preserve the exact dummy value.
         *
         * Example:
         *
         *     Hash("DUMMY_PASSWORD")
         *
         * is safe to reconstruct.
         *
         * But:
         *
         *     Hash("DUMMY_PASSWORD + SALT")
         *
         * modifies the protected value and therefore requires review.
         */
        int dummyStart =
            change.NewText.IndexOf(
                protectedRegion.DummyText,
                StringComparison.Ordinal
            );

        if (dummyStart < 0)
        {
            return new ReversePatch
            {
                FilePath = change.FilePath,
                Start = realStart,
                Length = realLength,
                OriginalText = realOriginalText,
                ReplacementText = realOriginalText,
                RequiresReview = true,
                Reason =
                    "The LLM removed or modified the protected dummy "
                    + "value, so automatic reverse mapping is unsafe."
            };
        }

        string replacementText =
            change.NewText
                .Remove(
                    dummyStart,
                    protectedRegion.DummyText.Length
                )
                .Insert(
                    dummyStart,
                    protectedRegion.OriginalText
                );

        return new ReversePatch
        {
            FilePath = change.FilePath,
            Start = realStart,
            Length = realLength,
            OriginalText = realOriginalText,
            ReplacementText = replacementText,
            RequiresReview = false,
            Reason =
                "Protected value was preserved by the LLM and "
                + "was reconstructed from the original source mapping."
        };
    }

    private static ProtectedRegion? FindProtectedRegion(
        SanitizedChange change,
        SanitizationMapping mapping
    )
    {
        int changeStart =
            change.Start;

        int changeEnd =
            change.Start + change.OriginalLength;

        int mappingStart =
            mapping.SanitizedStart;

        int mappingEnd =
            mapping.SanitizedStart
            + mapping.SanitizedLength;

        if (
            mappingStart >= changeEnd
            || mappingEnd <= changeStart
        )
        {
            return null;
        }

        int overlapStart =
            Math.Max(
                changeStart,
                mappingStart
            );

        int overlapEnd =
            Math.Min(
                changeEnd,
                mappingEnd
            );

        return new ProtectedRegion
        {
            Start =
                overlapStart - changeStart,

            Length =
                overlapEnd - overlapStart,

            DummyText =
                mapping.DummyText,

            OriginalText =
                mapping.OriginalText,

            NodeId =
                mapping.NodeId,
        };
    }

    private static int TranslateSanitizedPositionToOriginal(
        int sanitizedPosition,
        IEnumerable<SanitizationMapping> mappings
    )
    {
        int originalPosition =
            sanitizedPosition;

        foreach (SanitizationMapping mapping in mappings)
        {
            int sanitizedStart =
                mapping.SanitizedStart;

            int sanitizedEnd =
                mapping.SanitizedStart
                + mapping.SanitizedLength;

            if (sanitizedPosition < sanitizedStart)
            {
                break;
            }

            if (sanitizedPosition >= sanitizedEnd)
            {
                originalPosition -=
                    mapping.SanitizedLength
                    - mapping.OriginalLength;

                continue;
            }

            /*
             * The position falls inside a sanitized protected region.
             *
             * There is no one-to-one coordinate mapping inside the
             * replaced value. For the alpha implementation, map it
             * to the beginning of the corresponding original value.
             */
            originalPosition =
                mapping.OriginalStart;

            break;
        }

        return originalPosition;
    }

    private static string ReconstructOriginalText(
        string sanitizedText,
        IEnumerable<SanitizationMapping> mappings
    )
    {
        string result = sanitizedText;

        /*
         * We only care about mappings whose dummy text actually
         * occurs inside this particular changed region.
         *
         * We replace the actual dummy text rather than calculating
         * offsets from the entire source.
         */
        foreach (
            SanitizationMapping mapping in mappings
                .OrderByDescending(
                    mapping => mapping.SanitizedStart
                )
        )
        {
            int dummyStart =
                result.IndexOf(
                    mapping.DummyText,
                    StringComparison.Ordinal
                );

            if (dummyStart < 0)
            {
                continue;
            }

            result =
                result
                    .Remove(
                        dummyStart,
                        mapping.DummyText.Length
                    )
                    .Insert(
                        dummyStart,
                        mapping.OriginalText
                    );
        }

        return result;
    }
}