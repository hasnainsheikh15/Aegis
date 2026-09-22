using Aegis.Sanitizer;
using Aegis.Sanitizer.Models;
using RoslynWorker;
using RoslynWorker.Models;
using Xunit;

namespace Sanitizer.Tests;

public sealed class PatchApplicationServiceTests
{
    [Fact]
    public void Apply_ValidPatch_WritesPatchedSource()
    {
        string filePath =
            Path.Combine(
                Path.GetTempPath(),
                $"aegis-{Guid.NewGuid():N}.cs"
            );

        try
        {
            string originalSource =
                "class Test\r\n" +
                "{\r\n" +
                "    string value = \"abc\";\r\n" +
                "}\r\n";

            File.WriteAllText(
                filePath,
                originalSource
            );

            SourceHasher sourceHasher = new();

            ReversePatch patch =
                new()
                {
                    FilePath = filePath,
                    Start = originalSource.IndexOf("\"abc\""),
                    Length = "\"abc\"".Length,
                    OriginalText = "\"abc\"",
                    ReplacementText = "\"changed\"",
                    RequiresReview = false,
                    Reason = ""
                };

            PatchApplicationService service =
                new(
                    new PatchApplier(),
                    new RoslynWorker.Validation.PatchValidator(),
                    sourceHasher
                );

            PatchApplicationResult result =
                service.Apply(
                    filePath,
                    sourceHasher.Compute(originalSource),
                    [patch]
                );

            Assert.True(result.Applied);
            Assert.False(result.RequiresReview);

            string actual =
                File.ReadAllText(filePath);

            Assert.Contains(
                "string value = \"changed\";",
                actual
            );
        }
        finally
        {
            DeleteIfExists(filePath);
        }
    }

    [Fact]
    public void Apply_ReviewRequiredPatch_DoesNotModifyFile()
    {
        string filePath =
            Path.Combine(
                Path.GetTempPath(),
                $"aegis-{Guid.NewGuid():N}.cs"
            );

        try
        {
            string originalSource =
                "class Test\r\n" +
                "{\r\n" +
                "    string value = \"abc\";\r\n" +
                "}\r\n";

            File.WriteAllText(
                filePath,
                originalSource
            );

            SourceHasher sourceHasher = new();

            ReversePatch patch =
                new()
                {
                    FilePath = filePath,
                    Start = originalSource.IndexOf("\"abc\""),
                    Length = "\"abc\"".Length,
                    OriginalText = "\"abc\"",
                    ReplacementText = "\"changed\"",
                    RequiresReview = true,
                    Reason = "Protected value was modified."
                };

            PatchApplicationService service =
                new(
                    new PatchApplier(),
                    new RoslynWorker.Validation.PatchValidator(),
                    sourceHasher
                );

            PatchApplicationResult result =
                service.Apply(
                    filePath,
                    sourceHasher.Compute(originalSource),
                    [patch]
                );

            Assert.False(result.Applied);
            Assert.True(result.RequiresReview);

            Assert.Equal(
                originalSource,
                File.ReadAllText(filePath)
            );
        }
        finally
        {
            DeleteIfExists(filePath);
        }
    }

    [Fact]
    public void Apply_SourceChangedAfterSession_DoesNotModifyFile()
    {
        string filePath =
            Path.Combine(
                Path.GetTempPath(),
                $"aegis-{Guid.NewGuid():N}.cs"
            );

        try
        {
            string sessionSource =
                "class Test\r\n" +
                "{\r\n" +
                "    string value = \"abc\";\r\n" +
                "}\r\n";

            string currentSource =
                "class Test\r\n" +
                "{\r\n" +
                "    string value = \"someone-changed-this\";\r\n" +
                "}\r\n";

            File.WriteAllText(
                filePath,
                currentSource
            );

            SourceHasher sourceHasher = new();

            ReversePatch patch =
                new()
                {
                    FilePath = filePath,
                    Start = sessionSource.IndexOf("\"abc\""),
                    Length = "\"abc\"".Length,
                    OriginalText = "\"abc\"",
                    ReplacementText = "\"changed\"",
                    RequiresReview = false,
                    Reason = ""
                };

            PatchApplicationService service =
                new(
                    new PatchApplier(),
                    new RoslynWorker.Validation.PatchValidator(),
                    sourceHasher
                );

            PatchApplicationResult result =
                service.Apply(
                    filePath,
                    sourceHasher.Compute(sessionSource),
                    [patch]
                );

            Assert.False(result.Applied);
            Assert.True(result.RequiresReview);

            Assert.Equal(
                currentSource,
                File.ReadAllText(filePath)
            );
        }
        finally
        {
            DeleteIfExists(filePath);
        }
    }

    [Fact]
    public void Apply_InvalidPatchedSource_DoesNotModifyFile()
    {
        string filePath =
            Path.Combine(
                Path.GetTempPath(),
                $"aegis-{Guid.NewGuid():N}.cs"
            );

        try
        {
            string originalSource =
                "class Test\r\n" +
                "{\r\n" +
                "    string value = \"abc\";\r\n" +
                "}\r\n";

            File.WriteAllText(
                filePath,
                originalSource
            );

            SourceHasher sourceHasher = new();

            ReversePatch patch =
                new()
                {
                    FilePath = filePath,
                    Start = originalSource.IndexOf("\"abc\""),
                    Length = "\"abc\"".Length,
                    OriginalText = "\"abc\"",
                    ReplacementText = "\"broken;\r\n",
                    RequiresReview = false,
                    Reason = ""
                };

            PatchApplicationService service =
                new(
                    new PatchApplier(),
                    new RoslynWorker.Validation.PatchValidator(),
                    sourceHasher
                );

            PatchApplicationResult result =
                service.Apply(
                    filePath,
                    sourceHasher.Compute(originalSource),
                    [patch]
                );

            Assert.False(result.Applied);
            Assert.True(result.RequiresReview);
            Assert.Contains(
                "syntax",
                result.Message,
                StringComparison.OrdinalIgnoreCase
            );

            Assert.Equal(
                originalSource,
                File.ReadAllText(filePath)
            );
        }
        finally
        {
            DeleteIfExists(filePath);
        }
    }

    private static void DeleteIfExists(string filePath)
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }
}