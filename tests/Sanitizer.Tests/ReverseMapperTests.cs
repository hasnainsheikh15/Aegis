using Aegis.Sanitizer;
using Aegis.Sanitizer.Models;

namespace Sanitizer.Tests;

public sealed class ReverseMapperTests
{
    [Fact]
    public void ProtectedValuePreserved_IsAutomaticallyReconstructed()
    {
        string originalSource = "private string password = \"abc123\";\r\n";

        string sanitizedSource = "private string password = \"DUMMY_PASSWORD\";\r\n";

        string modifiedSanitizedSource = "private string password = Hash(\"DUMMY_PASSWORD\");\r\n";

        SanitizationMapping mapping = new()
        {
            NodeId = "password-node",
            OriginalText = "\"abc123\"",
            DummyText = "\"DUMMY_PASSWORD\"",
            FilePath = "Program.cs",
            OriginalStart = 26,
            OriginalLength = "\"abc123\"".Length,
            SanitizedStart = 26,
            SanitizedLength = "\"DUMMY_PASSWORD\"".Length,
        };

        SanitizedChangeDetector detector = new();

        List<SanitizedChange> changes = detector.Detect(
            sanitizedSource,
            modifiedSanitizedSource,
            "Program.cs"
        );

        ReverseMapper mapper = new();

        List<ReverseMappedChange> analyzed = mapper.Analyze(changes, [mapping]);

        ReversePatch? patch = mapper.CreatePatch(analyzed[0], [mapping], originalSource);

        Assert.NotNull(patch);

        Assert.False(patch.RequiresReview);

        Assert.Equal("private string password = \"abc123\";\r\n", patch.OriginalText);

        Assert.Equal("private string password = Hash(\"abc123\");\r\n", patch.ReplacementText);
    }

    [Fact]
    public void ProtectedValueRemoved_RequiresReview()
    {
        string originalSource = "private string password = \"abc123\";\r\n";

        string sanitizedSource = "private string password = \"DUMMY_PASSWORD\";\r\n";

        string modifiedSanitizedSource = "private string password = \"\";\r\n";

        SanitizationMapping mapping = new()
        {
            NodeId = "password-node",
            OriginalText = "\"abc123\"",
            DummyText = "\"DUMMY_PASSWORD\"",
            FilePath = "Program.cs",
            OriginalStart = 29,
            OriginalLength = "\"abc123\"".Length,
            SanitizedStart = 29,
            SanitizedLength = "\"DUMMY_PASSWORD\"".Length,
        };

        SanitizedChangeDetector detector = new();

        List<SanitizedChange> changes = detector.Detect(
            sanitizedSource,
            modifiedSanitizedSource,
            "Program.cs"
        );

        ReverseMapper mapper = new();

        List<ReverseMappedChange> analyzed = mapper.Analyze(changes, [mapping]);

        ReversePatch? patch = mapper.CreatePatch(analyzed[0], [mapping], originalSource);

        Assert.NotNull(patch);
        Assert.True(patch.RequiresReview);
    }

    [Fact]
    public void ProtectedValueModified_RequiresReview()
    {
        string originalSource = "private string password = \"abc123\";\r\n";

        string sanitizedSource = "private string password = \"DUMMY_PASSWORD\";\r\n";

        string modifiedSanitizedSource = "private string password = \"DUMMY_PASSWORD_SALT\";\r\n";

        SanitizationMapping mapping = new()
        {
            NodeId = "password-node",
            OriginalText = "\"abc123\"",
            DummyText = "\"DUMMY_PASSWORD\"",
            FilePath = "Program.cs",
            OriginalStart = 29,
            OriginalLength = "\"abc123\"".Length,
            SanitizedStart = 29,
            SanitizedLength = "\"DUMMY_PASSWORD\"".Length,
        };

        SanitizedChangeDetector detector = new();

        List<SanitizedChange> changes = detector.Detect(
            sanitizedSource,
            modifiedSanitizedSource,
            "Program.cs"
        );

        ReverseMapper mapper = new();

        List<ReverseMappedChange> analyzed = mapper.Analyze(changes, [mapping]);

        ReversePatch? patch = mapper.CreatePatch(analyzed[0], [mapping], originalSource);

        Assert.NotNull(patch);
        Assert.True(patch.RequiresReview);
    }

    [Fact]
    public void UnrelatedChange_IsAutomaticallyPatchable()
    {
        string originalSource =
            "private string password = \"abc123\";\r\n" + "string token = password;\r\n";

        string sanitizedSource =
            "private string password = \"DUMMY_PASSWORD\";\r\n" + "string token = password;\r\n";

        string modifiedSanitizedSource =
            "private string password = \"DUMMY_PASSWORD\";\r\n"
            + "string token = Hash(password);\r\n";

        SanitizationMapping mapping = new()
        {
            NodeId = "password-node",
            OriginalText = "\"abc123\"",
            DummyText = "\"DUMMY_PASSWORD\"",
            FilePath = "Program.cs",
            OriginalStart = 29,
            OriginalLength = "\"abc123\"".Length,
            SanitizedStart = 29,
            SanitizedLength = "\"DUMMY_PASSWORD\"".Length,
        };

        SanitizedChangeDetector detector = new();

        List<SanitizedChange> changes = detector.Detect(
            sanitizedSource,
            modifiedSanitizedSource,
            "Program.cs"
        );

        ReverseMapper mapper = new();

        List<ReverseMappedChange> analyzed = mapper.Analyze(changes, [mapping]);

        Assert.Single(analyzed);

        ReversePatch? patch = mapper.CreatePatch(analyzed[0], [mapping], originalSource);

        Assert.NotNull(patch);

        Assert.False(patch.RequiresReview);

        Assert.Equal("string token = password;\r\n", patch.OriginalText);

        Assert.Equal("string token = Hash(password);\r\n", patch.ReplacementText);
    }

    [Fact]
    public void ChangeAfterProtectedValue_UsesCorrectOriginalCoordinates()
    {
        string originalSource =
            "private string password = \"abc123\";\r\n"
            + "string token = password;\r\n"
            + "string backup = token;\r\n";

        string sanitizedSource =
            "private string password = \"DUMMY_PASSWORD\";\r\n"
            + "string token = password;\r\n"
            + "string backup = token;\r\n";

        string modifiedSanitizedSource =
            "private string password = \"DUMMY_PASSWORD\";\r\n"
            + "string token = password;\r\n"
            + "string backup = Encrypt(token);\r\n";

        SanitizationMapping mapping = new()
        {
            NodeId = "password-node",
            OriginalText = "\"abc123\"",
            DummyText = "\"DUMMY_PASSWORD\"",
            FilePath = "Program.cs",
            OriginalStart = 29,
            OriginalLength = "\"abc123\"".Length,
            SanitizedStart = 29,
            SanitizedLength = "\"DUMMY_PASSWORD\"".Length,
        };

        SanitizedChangeDetector detector = new();

        List<SanitizedChange> changes = detector.Detect(
            sanitizedSource,
            modifiedSanitizedSource,
            "Program.cs"
        );

        ReverseMapper mapper = new();

        List<ReverseMappedChange> analyzed = mapper.Analyze(changes, [mapping]);

        Assert.Single(analyzed);

        ReversePatch? patch = mapper.CreatePatch(analyzed[0], [mapping], originalSource);

        Assert.NotNull(patch);

        Assert.False(patch.RequiresReview);

        Assert.Equal("string backup = token;\r\n", patch.OriginalText);

        Assert.Equal("string backup = Encrypt(token);\r\n", patch.ReplacementText);
    }

    [Fact]
    public void MultipleProtectedValuesInSameChange_RequiresReview()
    {
        string originalSource = "string a = \"secret-a\"; string b = \"secret-b\";\r\n";

        string sanitizedSource = "string a = \"DUMMY_A\"; string b = \"DUMMY_B\";\r\n";

        string modifiedSanitizedSource =
            "string a = Hash(\"DUMMY_A\"); string b = Hash(\"DUMMY_B\");\r\n";

        SanitizationMapping mappingA = new()
        {
            NodeId = "a-node",
            OriginalText = "\"secret-a\"",
            DummyText = "\"DUMMY_A\"",
            FilePath = "Program.cs",
            OriginalStart = 11,
            OriginalLength = "\"secret-a\"".Length,
            SanitizedStart = 11,
            SanitizedLength = "\"DUMMY_A\"".Length,
        };

        SanitizationMapping mappingB = new()
        {
            NodeId = "b-node",
            OriginalText = "\"secret-b\"",
            DummyText = "\"DUMMY_B\"",
            FilePath = "Program.cs",
            OriginalStart = 34,
            OriginalLength = "\"secret-b\"".Length,
            SanitizedStart = 34,
            SanitizedLength = "\"DUMMY_B\"".Length,
        };

        SanitizedChangeDetector detector = new();

        List<SanitizedChange> changes = detector.Detect(
            sanitizedSource,
            modifiedSanitizedSource,
            "Program.cs"
        );

        ReverseMapper mapper = new();

        List<ReverseMappedChange> analyzed = mapper.Analyze(changes, [mappingA, mappingB]);

        Assert.Single(analyzed);

        ReversePatch? patch = mapper.CreatePatch(analyzed[0], [mappingA, mappingB], originalSource);

        Assert.NotNull(patch);

        Assert.True(patch.RequiresReview);
    }

    [Fact]
    public void ProtectedDummyAppearingTwice_RequiresReview()
    {
        string originalSource = "string value = \"abc123\";\r\n";

        string sanitizedSource = "string value = \"DUMMY_SECRET\";\r\n";

        string modifiedSanitizedSource =
            "string value = Combine(\"DUMMY_SECRET\", \"DUMMY_SECRET\");\r\n";

        SanitizationMapping mapping = new()
        {
            NodeId = "secret-node",
            OriginalText = "\"abc123\"",
            DummyText = "\"DUMMY_SECRET\"",
            FilePath = "Program.cs",
            OriginalStart = 15,
            OriginalLength = "\"abc123\"".Length,
            SanitizedStart = 15,
            SanitizedLength = "\"DUMMY_SECRET\"".Length,
        };

        SanitizedChangeDetector detector = new();

        List<SanitizedChange> changes = detector.Detect(
            sanitizedSource,
            modifiedSanitizedSource,
            "Program.cs"
        );

        ReverseMapper mapper = new();

        List<ReverseMappedChange> analyzed = mapper.Analyze(changes, [mapping]);

        ReversePatch? patch = mapper.CreatePatch(analyzed[0], [mapping], originalSource);

        Assert.NotNull(patch);

        Assert.True(patch.RequiresReview);
    }

    [Fact]
    public void ProtectedValueReplacedWithDifferentValue_RequiresReview()
    {
        string originalSource = "private string password = \"abc123\";\r\n";

        string sanitizedSource = "private string password = \"DUMMY_PASSWORD\";\r\n";

        string modifiedSanitizedSource = "private string password = \"totally-different\";\r\n";

        SanitizationMapping mapping = new()
        {
            NodeId = "password-node",
            OriginalText = "\"abc123\"",
            DummyText = "\"DUMMY_PASSWORD\"",
            FilePath = "Program.cs",
            OriginalStart = 26,
            OriginalLength = "\"abc123\"".Length,
            SanitizedStart = 26,
            SanitizedLength = "\"DUMMY_PASSWORD\"".Length,
        };

        SanitizedChangeDetector detector = new();

        List<SanitizedChange> changes = detector.Detect(
            sanitizedSource,
            modifiedSanitizedSource,
            "Program.cs"
        );

        ReverseMapper mapper = new();

        List<ReverseMappedChange> analyzed = mapper.Analyze(changes, [mapping]);

        ReversePatch? patch = mapper.CreatePatch(analyzed[0], [mapping], originalSource);

        Assert.NotNull(patch);
        Assert.True(patch.RequiresReview);

        Assert.Equal(originalSource, patch.ReplacementText);
    }

    [Fact]
    public void ProtectedDummyModifiedByAppendingText_RequiresReview()
    {
        string originalSource = "private string password = \"abc123\";\r\n";

        string sanitizedSource = "private string password = \"DUMMY_PASSWORD\";\r\n";

        string modifiedSanitizedSource = "private string password = \"DUMMY_PASSWORD_SUFFIX\";\r\n";

        SanitizationMapping mapping = new()
        {
            NodeId = "password-node",
            OriginalText = "\"abc123\"",
            DummyText = "\"DUMMY_PASSWORD\"",
            FilePath = "Program.cs",
            OriginalStart = 26,
            OriginalLength = "\"abc123\"".Length,
            SanitizedStart = 26,
            SanitizedLength = "\"DUMMY_PASSWORD\"".Length,
        };

        SanitizedChangeDetector detector = new();

        List<SanitizedChange> changes = detector.Detect(
            sanitizedSource,
            modifiedSanitizedSource,
            "Program.cs"
        );

        ReverseMapper mapper = new();

        List<ReverseMappedChange> analyzed = mapper.Analyze(changes, [mapping]);

        ReversePatch? patch = mapper.CreatePatch(analyzed[0], [mapping], originalSource);

        Assert.NotNull(patch);
        Assert.True(patch.RequiresReview);
    }

    [Fact]
    public void OriginalSourceChangedAfterAnalysis_RequiresReview()
    {
        // string analyzedOriginalSource = "private string password = \"abc123\";\r\n";

        string currentOriginalSource = "private string password = \"CHANGED_LOCALLY\";\r\n";

        string sanitizedSource = "private string password = \"DUMMY_PASSWORD\";\r\n";

        string modifiedSanitizedSource = "private string password = Hash(\"DUMMY_PASSWORD\");\r\n";

        SanitizationMapping mapping = new()
        {
            NodeId = "password-node",
            OriginalText = "\"abc123\"",
            DummyText = "\"DUMMY_PASSWORD\"",
            FilePath = "Program.cs",
            OriginalStart = 26,
            OriginalLength = "\"abc123\"".Length,
            SanitizedStart = 26,
            SanitizedLength = "\"DUMMY_PASSWORD\"".Length,
        };

        SanitizedChangeDetector detector = new();

        List<SanitizedChange> changes = detector.Detect(
            sanitizedSource,
            modifiedSanitizedSource,
            "Program.cs"
        );

        ReverseMapper mapper = new();

        List<ReverseMappedChange> analyzed = mapper.Analyze(changes, [mapping]);

        ReversePatch? patch = mapper.CreatePatch(analyzed[0], [mapping], currentOriginalSource);

        Assert.NotNull(patch);

        Assert.True(patch.RequiresReview);

        Assert.Contains("does not match", patch.Reason, StringComparison.OrdinalIgnoreCase);
    }
}
