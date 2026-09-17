using Aegis.Sanitizer.Models;

namespace Aegis.Sanitizer;

public sealed class ReverseMapper
{
    public List<ReverseMappedChange> Analyze(
        IEnumerable<SanitizedChange> changes,
        IEnumerable<SanitizationMapping> mappings
    )
    {
        List<SanitizationMapping> mappingList =
            mappings
                .OrderBy(mapping => mapping.SanitizedStart)
                .ToList();

        List<ReverseMappedChange> results = [];

        foreach (SanitizedChange change in changes)
        {
            List<ProtectedRegion> protectedRegions = [];

            foreach (SanitizationMapping mapping in mappingList)
            {
                ProtectedRegion? region =
                    FindProtectedRegion(
                        change,
                        mapping
                    );

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
         * Translate the beginning of the changed region from
         * sanitized-source coordinates to original-source
         * coordinates.
         */
        int realStart =
            TranslateSanitizedPositionToOriginal(
                change.Start,
                mappingList
            );

        /*
         * Reconstruct exactly what existed in the original
         * source for this changed region.
         *
         * Only mappings completely contained inside this
         * change are allowed to participate.
         *
         * If a protected region is only partially covered by
         * the change, automatic reconstruction is unsafe.
         */
        bool reconstructionFailed;

        string? realOriginalText =
            ReconstructOriginalText(
                change,
                mappingList,
                out reconstructionFailed
            );

        if (
            reconstructionFailed
            || realOriginalText is null
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
                    "The changed region does not align safely "
                    + "with the sanitization mappings."
            };
        }

        int realLength =
            realOriginalText.Length;

        /*
         * Verify that the calculated original range is valid.
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
         * Strong consistency check.
         *
         * The text calculated from the sanitization mappings
         * MUST exactly equal the text currently present in the
         * original source.
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

        /*
         * Protected changes require special handling because
         * the real protected value must NEVER come from the LLM.
         */
        if (!mappedChange.IsSafe)
        {
            return CreateProtectedPatch(
                mappedChange,
                realStart,
                realLength,
                sourceOriginalText
            );
        }

        /*
         * No protected region is involved.
         *
         * The LLM's sanitized change can therefore be used
         * directly as the replacement text.
         */
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

    private static ReversePatch CreateProtectedPatch(
        ReverseMappedChange mappedChange,
        int realStart,
        int realLength,
        string realOriginalText
    )
    {
        SanitizedChange change =
            mappedChange.SourceChange;

        /*
         * Alpha safety rule:
         *
         * If multiple protected regions occur inside the same
         * changed region, do not attempt automatic reconstruction.
         */
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
                    "Change contains multiple protected regions "
                    + "and cannot be safely reconstructed "
                    + "automatically."
            };
        }

        ProtectedRegion protectedRegion =
            mappedChange.ProtectedRegions[0];

        /*
         * The exact dummy value must still exist in the LLM's
         * output.
         *
         * Example:
         *
         *     Hash("DUMMY_PASSWORD")
         *
         * is acceptable.
         *
         * But:
         *
         *     Hash("DUMMY_PASSWORD" + salt)
         *
         * means the protected value was modified and therefore
         * requires human review.
         */
        int firstDummyStart =
            change.NewText.IndexOf(
                protectedRegion.DummyText,
                StringComparison.Ordinal
            );

        int lastDummyStart =
            change.NewText.LastIndexOf(
                protectedRegion.DummyText,
                StringComparison.Ordinal
            );

        /*
         * Dummy must exist exactly once.
         *
         * Zero occurrences means the LLM removed or modified it.
         *
         * Multiple occurrences are ambiguous and therefore
         * unsafe to reconstruct automatically.
         */
        if (
            firstDummyStart < 0
            || firstDummyStart != lastDummyStart
        )
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
                    "The protected dummy value was removed, "
                    + "modified, or appears multiple times in "
                    + "the LLM output."
            };
        }

        /*
         * Replace ONLY the dummy value with the original value.
         *
         * The original secret therefore comes exclusively from
         * Aegis's local mapping.
         */
        string replacementText =
            change.NewText
                .Remove(
                    firstDummyStart,
                    protectedRegion.DummyText.Length
                )
                .Insert(
                    firstDummyStart,
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
                + "was reconstructed from the original source "
                + "mapping."
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

        /*
         * No overlap.
         */
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
        IReadOnlyList<SanitizationMapping> mappings
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

            /*
             * This mapping occurs after the position we're
             * translating.
             */
            if (sanitizedPosition < sanitizedStart)
            {
                break;
            }

            /*
             * This entire mapping occurs before the position.
             *
             * Account for the difference between the sanitized
             * and original lengths.
             */
            if (sanitizedPosition >= sanitizedEnd)
            {
                originalPosition -=
                    mapping.SanitizedLength
                    - mapping.OriginalLength;

                continue;
            }

            /*
             * The position falls inside a protected region.
             *
             * There is no one-to-one mapping inside the replaced
             * value. For the alpha, map to the beginning of the
             * original protected value.
             */
            originalPosition =
                mapping.OriginalStart;

            break;
        }

        return originalPosition;
    }

    private static string? ReconstructOriginalText(
        SanitizedChange change,
        IReadOnlyList<SanitizationMapping> mappings,
        out bool failed
    )
    {
        failed = false;

        string result =
            change.OriginalText;

        int changeStart =
            change.Start;

        int changeEnd =
            change.Start + change.OriginalLength;

        /*
         * Find mappings that are completely contained inside
         * this changed region.
         */
        List<SanitizationMapping> containedMappings =
            mappings
                .Where(mapping =>
                {
                    int mappingStart =
                        mapping.SanitizedStart;

                    int mappingEnd =
                        mapping.SanitizedStart
                        + mapping.SanitizedLength;

                    return
                        mappingStart >= changeStart
                        && mappingEnd <= changeEnd;
                })
                .OrderByDescending(
                    mapping => mapping.SanitizedStart
                )
                .ToList();

        /*
         * If a mapping overlaps the change but is not completely
         * contained inside it, the diff split through a protected
         * region.
         *
         * We cannot safely reconstruct that automatically.
         */
        foreach (SanitizationMapping mapping in mappings)
        {
            int mappingStart =
                mapping.SanitizedStart;

            int mappingEnd =
                mapping.SanitizedStart
                + mapping.SanitizedLength;

            bool overlaps =
                mappingStart < changeEnd
                && mappingEnd > changeStart;

            bool contained =
                mappingStart >= changeStart
                && mappingEnd <= changeEnd;

            if (overlaps && !contained)
            {
                failed = true;
                return null;
            }
        }

        /*
         * Process from right to left.
         *
         * Replacing a mapping on the right changes only positions
         * after mappings on its left, so the relative positions of
         * the remaining mappings stay valid.
         */
        foreach (SanitizationMapping mapping in containedMappings)
        {
            int relativeStart =
                mapping.SanitizedStart - changeStart;

            if (
                relativeStart < 0
                || relativeStart + mapping.SanitizedLength
                    > result.Length
            )
            {
                failed = true;
                return null;
            }

            /*
             * Verify that the text at the expected coordinate is
             * actually the dummy text.
             */
            string actualDummy =
                result.Substring(
                    relativeStart,
                    mapping.SanitizedLength
                );

            if (actualDummy != mapping.DummyText)
            {
                failed = true;
                return null;
            }

            result =
                result
                    .Remove(
                        relativeStart,
                        mapping.SanitizedLength
                    )
                    .Insert(
                        relativeStart,
                        mapping.OriginalText
                    );
        }

        return result;
    }
}