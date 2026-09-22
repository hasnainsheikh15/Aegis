using Aegis.Sanitizer;
using Aegis.Sanitizer.Models;
using Aegis.Sanitizer.Models.Import;
using Aegis.Sanitizer.Models.Session;

namespace Sanitizer.Tests;

public sealed class SessionImporterTests
{
    [Fact]
    public void Import_SafeChange_ProducesReversePatch()
    {
        string root = Path.Combine(Path.GetTempPath(), "aegis-test-" + Guid.NewGuid());

        Directory.CreateDirectory(root);

        try
        {
            string originalPath = Path.Combine(root, "Program.cs");

            string baselinePath = Path.Combine(root, "baseline", "Program.cs");

            string sanitizedPath = Path.Combine(root, "sanitized", "Program.cs");

            Directory.CreateDirectory(Path.GetDirectoryName(baselinePath)!);

            Directory.CreateDirectory(Path.GetDirectoryName(sanitizedPath)!);

            string originalSource = """
                public class AuthService
                {
                    private string password = "abc123";

                    public void Login()
                    {
                        string token = password;
                    }
                }
                """;

            string baselineSanitizedSource = """
                public class AuthService
                {
                    private string password = "DUMMY_PASSWORD";

                    public void Login()
                    {
                        string token = password;
                    }
                }
                """;

            string modifiedSanitizedSource = """
                public class AuthService
                {
                    private string password = "DUMMY_PASSWORD";

                    public void Login()
                    {
                        string token = Hash(password);
                    }
                }
                """;

            File.WriteAllText(originalPath, originalSource);

            File.WriteAllText(baselinePath, baselineSanitizedSource);

            File.WriteAllText(sanitizedPath, modifiedSanitizedSource);

            SourceHasher hasher = new();

            SanitizationMapping mapping = new()
            {
                NodeId = "password-node",
                OriginalText = "\"abc123\"",
                DummyText = "\"DUMMY_PASSWORD\"",
                FilePath = originalPath,
                OriginalStart = originalSource.IndexOf("\"abc123\"", StringComparison.Ordinal),
                OriginalLength = "\"abc123\"".Length,
                SanitizedStart = baselineSanitizedSource.IndexOf(
                    "\"DUMMY_PASSWORD\"",
                    StringComparison.Ordinal
                ),
                SanitizedLength = "\"DUMMY_PASSWORD\"".Length,
            };

            AegisSession session = new()
            {
                SessionId = "test-session",
                Files =
                [
                    new SessionFile
                    {
                        OriginalFilePath = originalPath,
                        SanitizedFilePath = sanitizedPath,
                        BaselineSanitizedFilePath = baselinePath,
                        OriginalSourceHash = hasher.Compute(originalSource),
                        SanitizedSourceHash = hasher.Compute(baselineSanitizedSource),
                        Mappings =
                        [
                            new SessionMapping
                            {
                                NodeId = mapping.NodeId,
                                NodeType = Aegis.Pir.Enums.PirNodeType.Field,
                                OriginalText = mapping.OriginalText,
                                DummyText = mapping.DummyText,
                                OriginalStart = mapping.OriginalStart,
                                OriginalLength = mapping.OriginalLength,
                                SanitizedStart = mapping.SanitizedStart,
                                SanitizedLength = mapping.SanitizedLength,
                            },
                        ],
                    },
                ],
            };

            string sessionPath = Path.Combine(root, "session.json");

            SessionStore store = new();

            store.Save(session, sessionPath);

            SessionImporter importer = new(
                store,
                new SourceHasher(),
                new SanitizedChangeDetector(),
                new ReverseMapper()
            );

            ImportResult result = importer.Import(sessionPath);

            Assert.False(result.RequiresReview);

            ImportedFileResult file = Assert.Single(result.Files);

            ReversePatch patch = Assert.Single(file.Patches);

            Assert.False(patch.RequiresReview);

            Assert.Equal("        string token = password;\r\n", patch.OriginalText);

            Assert.Equal("        string token = Hash(password);\r\n", patch.ReplacementText);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void Import_ProtectedValueModified_RequiresReview()
    {
        string root = Path.Combine(Path.GetTempPath(), "aegis-test-" + Guid.NewGuid());

        Directory.CreateDirectory(root);

        try
        {
            string originalPath = Path.Combine(root, "Program.cs");

            string baselinePath = Path.Combine(root, "baseline", "Program.cs");

            string sanitizedPath = Path.Combine(root, "sanitized", "Program.cs");

            Directory.CreateDirectory(Path.GetDirectoryName(baselinePath)!);

            Directory.CreateDirectory(Path.GetDirectoryName(sanitizedPath)!);

            string originalSource = """
                public class AuthService
                {
                    private string password = "abc123";

                    public void Login()
                    {
                        string token = password;
                    }
                }
                """;

            string baselineSanitizedSource = """
                public class AuthService
                {
                    private string password = "DUMMY_PASSWORD";

                    public void Login()
                    {
                        string token = password;
                    }
                }
                """;

            /*
             * Simulate an unsafe LLM response.
             *
             * The protected dummy value has been changed.
             */
            string modifiedSanitizedSource = """
                public class AuthService
                {
                    private string password = "CHANGED_PASSWORD";

                    public void Login()
                    {
                        string token = password;
                    }
                }
                """;

            File.WriteAllText(originalPath, originalSource);

            File.WriteAllText(baselinePath, baselineSanitizedSource);

            File.WriteAllText(sanitizedPath, modifiedSanitizedSource);

            SourceHasher hasher = new();

            int originalPasswordStart = originalSource.IndexOf(
                "\"abc123\"",
                StringComparison.Ordinal
            );

            int sanitizedPasswordStart = baselineSanitizedSource.IndexOf(
                "\"DUMMY_PASSWORD\"",
                StringComparison.Ordinal
            );

            AegisSession session = new()
            {
                SessionId = "test-session",
                Files =
                [
                    new SessionFile
                    {
                        OriginalFilePath = originalPath,

                        SanitizedFilePath = sanitizedPath,

                        BaselineSanitizedFilePath = baselinePath,

                        OriginalSourceHash = hasher.Compute(originalSource),

                        SanitizedSourceHash = hasher.Compute(baselineSanitizedSource),

                        Mappings =
                        [
                            new SessionMapping
                            {
                                NodeId = "password-node",

                                NodeType = Aegis.Pir.Enums.PirNodeType.Field,

                                OriginalText = "\"abc123\"",

                                DummyText = "\"DUMMY_PASSWORD\"",

                                OriginalStart = originalPasswordStart,

                                OriginalLength = "\"abc123\"".Length,

                                SanitizedStart = sanitizedPasswordStart,

                                SanitizedLength = "\"DUMMY_PASSWORD\"".Length,
                            },
                        ],
                    },
                ],
            };

            string sessionPath = Path.Combine(root, "session.json");

            SessionStore store = new();

            store.Save(session, sessionPath);

            SessionImporter importer = new(
                store,
                new SourceHasher(),
                new SanitizedChangeDetector(),
                new ReverseMapper()
            );

            ImportResult result = importer.Import(sessionPath);

            ImportedFileResult file = Assert.Single(result.Files);

            Assert.True(file.RequiresReview);

            ReversePatch patch = Assert.Single(file.Patches);

            Assert.True(patch.RequiresReview);

            Assert.NotNull(patch.Reason);

            Assert.Contains("protected", patch.Reason!, StringComparison.OrdinalIgnoreCase);

            /*
             * The LLM's replacement must NOT become
             * an automatically applicable real-source patch.
             */
            Assert.Equal(patch.OriginalText, patch.ReplacementText);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
