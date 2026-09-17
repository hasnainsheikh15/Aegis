using Aegis.Sanitizer.Models;
using System.Text;

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
        sanitizedSource = source
            .Remove(target.Start, target.Length)
            .Insert(target.Start, dummyText);

        return new SanitizationMapping
        {
            NodeId = target.NodeId,
            OriginalText = target.OriginalText,
            DummyText = dummyText,
            FilePath = target.FilePath,
            OriginalStart = target.Start,
            OriginalLength = target.Length,
            SanitizedStart = target.Start,
            SanitizedLength = dummyText.Length,
        };
    }

    public List<SanitizationMapping> Sanitize(
        string source,
        List<SanitizationTarget> targets,
        Func<SanitizationTarget, string> dummyGenerator,
        out string sanitizedSource
    )
    {
        List<SanitizationMapping> mappings = [];

        StringBuilder builder = new();

        int currentPosition = 0;

        foreach (
            SanitizationTarget target
                in targets.OrderBy(target => target.Start)
        )
        {
            // Copy everything before this target.
            builder.Append(
                source,
                currentPosition,
                target.Start - currentPosition
            );

            string dummyText = dummyGenerator(target);

            int sanitizedStart = builder.Length;

            builder.Append(dummyText);

            mappings.Add(
                new SanitizationMapping
                {
                    NodeId = target.NodeId,
                    OriginalText = target.OriginalText,
                    DummyText = dummyText,
                    FilePath = target.FilePath,
                    OriginalStart = target.Start,
                    OriginalLength = target.Length,
                    SanitizedStart = sanitizedStart,
                    SanitizedLength = dummyText.Length,
                }
            );

            currentPosition =
                target.Start + target.Length;
        }

        // Copy the remaining source.
        builder.Append(
            source,
            currentPosition,
            source.Length - currentPosition
        );

        sanitizedSource = builder.ToString();

        return mappings;
    }
}