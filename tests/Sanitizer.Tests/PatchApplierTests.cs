using Aegis.Sanitizer;
using Aegis.Sanitizer.Models;

namespace Sanitizer.Tests;

public sealed class PatchApplierTests
{
    [Fact]
    public void Apply_SafePatch_ChangesSource()
    {
        PatchApplier applier = new();

        string source = "string token = password;\r\n";

        ReversePatch patch = new()
        {
            FilePath = "Program.cs",
            Start = 0,
            Length = source.Length,
            OriginalText = source,
            ReplacementText = "string token = Hash(password);\r\n",
            RequiresReview = false,
            Reason = "Safe change.",
        };

        string result = applier.Apply(source, [patch]);

        Assert.Equal("string token = Hash(password);\r\n", result);
    }

    [Fact]
    public void Apply_ReviewRequiredPatch_Throws()
    {
        PatchApplier applier = new();

        string source = "string password = \"abc123\";\r\n";

        ReversePatch patch = new()
        {
            FilePath = "Program.cs",
            Start = 0,
            Length = source.Length,
            OriginalText = source,
            ReplacementText = "string password = \"something\";\r\n",
            RequiresReview = true,
            Reason = "Protected value was modified.",
        };

        Assert.Throws<InvalidOperationException>(() => applier.Apply(source, [patch]));
    }

    [Fact]
    public void Apply_MultiplePatches_AppliesCorrectly()
    {
        PatchApplier applier = new();

        string source = "AAA\r\nBBB\r\nCCC\r\n";

        ReversePatch firstPatch = new()
        {
            FilePath = "Program.cs",
            Start = 0,
            Length = 5,
            OriginalText = "AAA\r\n",
            ReplacementText = "LONGER_AAA\r\n",
            RequiresReview = false,
            Reason = "",
        };

        ReversePatch secondPatch = new()
        {
            FilePath = "Program.cs",
            Start = 10,
            Length = 5,
            OriginalText = "CCC\r\n",
            ReplacementText = "X\r\n",
            RequiresReview = false,
            Reason = "",
        };

        string result = applier.Apply(source, [firstPatch, secondPatch]);

        Assert.Equal("LONGER_AAA\r\nBBB\r\nX\r\n", result);
    }

    [Fact]
    public void Apply_OriginalTextMismatch_Throws()
    {
        PatchApplier applier = new();

        string source = "string token = password;\r\n";

        ReversePatch patch = new()
        {
            FilePath = "Program.cs",
            Start = 0,
            Length = source.Length,
            OriginalText = "string token = somethingElse;\r\n",
            ReplacementText = "string token = Hash(password);\r\n",
            RequiresReview = false,
            Reason = "Safe change.",
        };

        Assert.Throws<InvalidOperationException>(() => applier.Apply(source, [patch]));
    }
}
