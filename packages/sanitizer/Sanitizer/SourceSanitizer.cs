using Aegis.Sanitizer.Models;

namespace Aegis.Sanitizer;

public sealed class SourceSanitizer
{
    public SanitizationMapping Sanitize(
        string sourceCode,
        SanitizationTarget target,
        string dummyText,
        out string sanitizedSource
    )
    {
        sanitizedSource =
            sourceCode.Remove(target.Start, target.Length)
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
}