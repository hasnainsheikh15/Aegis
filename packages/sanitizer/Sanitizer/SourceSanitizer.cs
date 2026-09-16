using Aegis.Sanitizer.Models;

namespace Aegis.Sanitizer;

public sealed class SourceSanitizer
{
    public SanitizationMapping Sanitize(
        string source,
        SanitizationTarget target,
        string dummyText,
        out string sanitizedSource
    )
    {
        sanitizedSource =
            source.Remove(target.Start, target.Length)
                  .Insert(target.Start, dummyText);

        return new SanitizationMapping
        {
            NodeId = target.NodeId,
            OriginalText = target.OriginalText,
            DummyText = dummyText,
            FilePath = target.FilePath,
            OriginalStart = target.Start,
            OriginalLength = target.Length,
        };
    }

    public List<SanitizationMapping> Sanitize(
        string source,
        IEnumerable<SanitizationTarget> targets,
        Func<SanitizationTarget, string> dummyGenerator,
        out string sanitizedSource
    )
    {
        List<SanitizationMapping> mappings = [];

        sanitizedSource = source;

        /*
         * Apply replacements from right to left.
         *
         * This is important because replacing text changes the
         * positions of everything that comes after it.
         */
        List<SanitizationTarget> orderedTargets =
            targets
                .OrderByDescending(target => target.Start)
                .ToList();

        foreach (SanitizationTarget target in orderedTargets)
        {
            string dummyText = dummyGenerator(target);

            sanitizedSource =
                sanitizedSource
                    .Remove(target.Start, target.Length)
                    .Insert(target.Start, dummyText);

            mappings.Add(new SanitizationMapping
            {
                NodeId = target.NodeId,
                OriginalText = target.OriginalText,
                DummyText = dummyText,
                FilePath = target.FilePath,
                OriginalStart = target.Start,
                OriginalLength = target.Length,
            });
        }

        return mappings;
    }
}