using Aegis.Pir.Enums;
using Aegis.Sanitizer;
using Aegis.Sanitizer.Models.Import;
using Aegis.Sanitizer.Models.Session;
using RoslynWorker;
using RoslynWorker.Models;
using RoslynWorker.Validation;

namespace Sanitizer.Tests;

public sealed class RoundTripTests
{
    [Fact]
    public void RoundTrip_SafeExternalChange_IsAppliedToRealSource()
    {
        string root = Path.Combine(Path.GetTempPath(), $"aegis-roundtrip-{Guid.NewGuid():N}");

        Directory.CreateDirectory(root);

        try
        {
            string originalFilePath = Path.Combine(root, "Program.cs");

            string sessionDirectory = Path.Combine(root, ".aegis", "sessions", "test-session");

            string sanitizedDirectory = Path.Combine(sessionDirectory, "sanitized");

            string baselineDirectory = Path.Combine(sessionDirectory, "baseline");

            Directory.CreateDirectory(sanitizedDirectory);
            Directory.CreateDirectory(baselineDirectory);

            string sanitizedFilePath = Path.Combine(sanitizedDirectory, "Program.cs");

            string baselineFilePath = Path.Combine(baselineDirectory, "Program.cs");

            string sessionFilePath = Path.Combine(sessionDirectory, "session.json");

            string originalSource =
                "public class AuthService\r\n"
                + "{\r\n"
                + "    private string password = \"abc123\";\r\n"
                + "\r\n"
                + "    public void Login()\r\n"
                + "    {\r\n"
                + "        string token = password;\r\n"
                + "    }\r\n"
                + "}\r\n";

            string sanitizedSource =
                "public class AuthService\r\n"
                + "{\r\n"
                + "    private string password = \"DUMMY_PASSWORD\";\r\n"
                + "\r\n"
                + "    public void Login()\r\n"
                + "    {\r\n"
                + "        string token = password;\r\n"
                + "    }\r\n"
                + "}\r\n";

            File.WriteAllText(originalFilePath, originalSource);

            File.WriteAllText(sanitizedFilePath, sanitizedSource);

            File.WriteAllText(baselineFilePath, sanitizedSource);

            int originalSecretStart = originalSource.IndexOf(
                "\"abc123\"",
                StringComparison.Ordinal
            );

            int sanitizedSecretStart = sanitizedSource.IndexOf(
                "\"DUMMY_PASSWORD\"",
                StringComparison.Ordinal
            );

            SourceHasher sourceHasher = new();

            AegisSession session = new()
            {
                Version = 1,
                SessionId = "test-session",
                Files =
                [
                    new SessionFile
                    {
                        OriginalFilePath = originalFilePath,
                        SanitizedFilePath = sanitizedFilePath,
                        BaselineSanitizedFilePath = baselineFilePath,
                        OriginalSourceHash = sourceHasher.Compute(originalSource),
                        SanitizedSourceHash = sourceHasher.Compute(sanitizedSource),
                        Mappings =
                        [
                            new SessionMapping
                            {
                                NodeId = "password-node",
                                NodeType = PirNodeType.Field,
                                OriginalText = "\"abc123\"",
                                DummyText = "\"DUMMY_PASSWORD\"",
                                OriginalStart = originalSecretStart,
                                OriginalLength = "\"abc123\"".Length,
                                SanitizedStart = sanitizedSecretStart,
                                SanitizedLength = "\"DUMMY_PASSWORD\"".Length,
                            },
                        ],
                    },
                ],
            };

            SessionStore sessionStore = new();

            sessionStore.Save(session, sessionFilePath);

            string modifiedSanitizedSource = sanitizedSource.Replace(
                "string token = password;",
                "string token = Hash(password);"
            );

            File.WriteAllText(sanitizedFilePath, modifiedSanitizedSource);

            SessionImporter importer = new(
                sessionStore,
                sourceHasher,
                new SanitizedChangeDetector(),
                new ReverseMapper()
            );

            ImportResult importResult = importer.Import(sessionFilePath);

            Assert.False(importResult.RequiresReview);
            Assert.Single(importResult.Files);

            ImportedFileResult importedFile = importResult.Files[0];

            Assert.False(importedFile.RequiresReview);
            Assert.NotEmpty(importedFile.Patches);

            PatchApplicationService applicationService = new(
                new PatchApplier(),
                new PatchValidator(),
                sourceHasher
            );

            PatchApplicationResult applicationResult = applicationService.Apply(
                originalFilePath,
                session.Files[0].OriginalSourceHash,
                importedFile.Patches
            );

            Assert.True(applicationResult.Applied);
            Assert.False(applicationResult.RequiresReview);

            string finalSource = File.ReadAllText(originalFilePath);

            Assert.Contains("private string password = \"abc123\";", finalSource);

            Assert.Contains("string token = Hash(password);", finalSource);

            Assert.DoesNotContain("DUMMY_PASSWORD", finalSource);
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
    public void RoundTrip_NestedExpressionChange_PreservesProtectedSecret()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            $"aegis-nested-roundtrip-{Guid.NewGuid():N}"
        );

        Directory.CreateDirectory(root);

        try
        {
            string originalFilePath = Path.Combine(root, "Program.cs");

            string sessionDirectory = Path.Combine(root, ".aegis", "sessions", "test-session");

            string sanitizedDirectory = Path.Combine(sessionDirectory, "sanitized");

            string baselineDirectory = Path.Combine(sessionDirectory, "baseline");

            Directory.CreateDirectory(sanitizedDirectory);
            Directory.CreateDirectory(baselineDirectory);

            string sanitizedFilePath = Path.Combine(sanitizedDirectory, "Program.cs");

            string baselineFilePath = Path.Combine(baselineDirectory, "Program.cs");

            string sessionFilePath = Path.Combine(sessionDirectory, "session.json");

            string originalSource =
                "public class AuthService\r\n"
                + "{\r\n"
                + "    private string password = \"abc123\";\r\n"
                + "\r\n"
                + "    public void Login()\r\n"
                + "    {\r\n"
                + "        string token = Hash(password).Trim();\r\n"
                + "        string backup = token;\r\n"
                + "    }\r\n"
                + "}\r\n";

            string sanitizedSource =
                "public class AuthService\r\n"
                + "{\r\n"
                + "    private string password = \"DUMMY_PASSWORD\";\r\n"
                + "\r\n"
                + "    public void Login()\r\n"
                + "    {\r\n"
                + "        string token = Hash(password).Trim();\r\n"
                + "        string backup = token;\r\n"
                + "    }\r\n"
                + "}\r\n";

            File.WriteAllText(originalFilePath, originalSource);

            File.WriteAllText(sanitizedFilePath, sanitizedSource);

            File.WriteAllText(baselineFilePath, sanitizedSource);

            SourceHasher sourceHasher = new();

            int originalSecretStart = originalSource.IndexOf(
                "\"abc123\"",
                StringComparison.Ordinal
            );

            int sanitizedSecretStart = sanitizedSource.IndexOf(
                "\"DUMMY_PASSWORD\"",
                StringComparison.Ordinal
            );

            AegisSession session = new()
            {
                Version = 1,
                SessionId = "test-session",
                Files =
                [
                    new SessionFile
                    {
                        OriginalFilePath = originalFilePath,

                        SanitizedFilePath = sanitizedFilePath,

                        BaselineSanitizedFilePath = baselineFilePath,

                        OriginalSourceHash = sourceHasher.Compute(originalSource),

                        SanitizedSourceHash = sourceHasher.Compute(sanitizedSource),

                        Mappings =
                        [
                            new SessionMapping
                            {
                                NodeId = "password-node",

                                NodeType = PirNodeType.Field,

                                OriginalText = "\"abc123\"",

                                DummyText = "\"DUMMY_PASSWORD\"",

                                OriginalStart = originalSecretStart,

                                OriginalLength = "\"abc123\"".Length,

                                SanitizedStart = sanitizedSecretStart,

                                SanitizedLength = "\"DUMMY_PASSWORD\"".Length,
                            },
                        ],
                    },
                ],
            };

            SessionStore sessionStore = new();

            sessionStore.Save(session, sessionFilePath);

            /*
             * Simulate the LLM modifying the surrounding
             * expression while leaving the protected dummy intact.
             */
            string modifiedSanitizedSource = sanitizedSource.Replace(
                "string token = Hash(password).Trim();",
                "string token = Hash(password).Trim().ToLower();"
            );

            File.WriteAllText(sanitizedFilePath, modifiedSanitizedSource);

            SessionImporter importer = new(
                sessionStore,
                sourceHasher,
                new SanitizedChangeDetector(),
                new ReverseMapper()
            );

            ImportResult importResult = importer.Import(sessionFilePath);

            Assert.False(importResult.RequiresReview);

            Assert.Single(importResult.Files);

            ImportedFileResult importedFile = importResult.Files[0];

            Assert.False(importedFile.RequiresReview);

            Assert.NotEmpty(importedFile.Patches);

            PatchApplicationService applicationService = new(
                new PatchApplier(),
                new PatchValidator(),
                sourceHasher
            );

            PatchApplicationResult applicationResult = applicationService.Apply(
                originalFilePath,
                session.Files[0].OriginalSourceHash,
                importedFile.Patches
            );

            Assert.True(applicationResult.Applied);

            Assert.False(applicationResult.RequiresReview);

            string finalSource = File.ReadAllText(originalFilePath);

            /*
             * The real secret must be restored from Aegis's
             * local mapping.
             */
            Assert.Contains("private string password = \"abc123\";", finalSource);

            /*
             * The LLM's legitimate modification must survive.
             */
            Assert.Contains("string token = Hash(password).Trim().ToLower();", finalSource);

            /*
             * The protected dummy must never reach real source.
             */
            Assert.DoesNotContain("DUMMY_PASSWORD", finalSource);

            /*
             * The surrounding flow must remain intact.
             */
            Assert.Contains("string backup = token;", finalSource);
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
    public void RoundTrip_MultipleProtectedValues_RestoresEachValueIndependently()
    {
        string root = Path.Combine(Path.GetTempPath(), $"aegis-multi-secret-{Guid.NewGuid():N}");

        Directory.CreateDirectory(root);

        try
        {
            string originalFilePath = Path.Combine(root, "Program.cs");

            string sessionDirectory = Path.Combine(root, ".aegis", "sessions", "test-session");

            string sanitizedDirectory = Path.Combine(sessionDirectory, "sanitized");

            string baselineDirectory = Path.Combine(sessionDirectory, "baseline");

            Directory.CreateDirectory(sanitizedDirectory);
            Directory.CreateDirectory(baselineDirectory);

            string sanitizedFilePath = Path.Combine(sanitizedDirectory, "Program.cs");

            string baselineFilePath = Path.Combine(baselineDirectory, "Program.cs");

            string sessionFilePath = Path.Combine(sessionDirectory, "session.json");

            string originalSource =
                "public class Config\r\n"
                + "{\r\n"
                + "    private string password = \"abc123\";\r\n"
                + "    private string apiKey = \"real-api-key\";\r\n"
                + "    private string secret = \"super-secret\";\r\n"
                + "\r\n"
                + "    public void Configure()\r\n"
                + "    {\r\n"
                + "        Use(password, apiKey, secret);\r\n"
                + "    }\r\n"
                + "}\r\n";

            string sanitizedSource =
                "public class Config\r\n"
                + "{\r\n"
                + "    private string password = \"DUMMY_PASSWORD\";\r\n"
                + "    private string apiKey = \"DUMMY_APIKEY\";\r\n"
                + "    private string secret = \"DUMMY_SECRET\";\r\n"
                + "\r\n"
                + "    public void Configure()\r\n"
                + "    {\r\n"
                + "        Use(password, apiKey, secret);\r\n"
                + "    }\r\n"
                + "}\r\n";

            File.WriteAllText(originalFilePath, originalSource);
            File.WriteAllText(sanitizedFilePath, sanitizedSource);
            File.WriteAllText(baselineFilePath, sanitizedSource);

            SourceHasher sourceHasher = new();

            SessionMapping CreateMapping(string nodeId, string originalText, string dummyText)
            {
                return new SessionMapping
                {
                    NodeId = nodeId,
                    NodeType = PirNodeType.Field,
                    OriginalText = originalText,
                    DummyText = dummyText,
                    OriginalStart = originalSource.IndexOf(originalText, StringComparison.Ordinal),
                    OriginalLength = originalText.Length,
                    SanitizedStart = sanitizedSource.IndexOf(dummyText, StringComparison.Ordinal),
                    SanitizedLength = dummyText.Length,
                };
            }

            AegisSession session = new()
            {
                Version = 1,
                SessionId = "test-session",
                Files =
                [
                    new SessionFile
                    {
                        OriginalFilePath = originalFilePath,
                        SanitizedFilePath = sanitizedFilePath,
                        BaselineSanitizedFilePath = baselineFilePath,
                        OriginalSourceHash = sourceHasher.Compute(originalSource),
                        SanitizedSourceHash = sourceHasher.Compute(sanitizedSource),

                        Mappings =
                        [
                            CreateMapping("password-node", "\"abc123\"", "\"DUMMY_PASSWORD\""),
                            CreateMapping("api-key-node", "\"real-api-key\"", "\"DUMMY_APIKEY\""),
                            CreateMapping("secret-node", "\"super-secret\"", "\"DUMMY_SECRET\""),
                        ],
                    },
                ],
            };

            SessionStore sessionStore = new();

            sessionStore.Save(session, sessionFilePath);

            // Simulate an LLM modifying surrounding code.
            string modifiedSanitizedSource = sanitizedSource.Replace(
                "Use(password, apiKey, secret);",
                "Use(password, apiKey, secret.ToUpper());"
            );

            File.WriteAllText(sanitizedFilePath, modifiedSanitizedSource);

            SessionImporter importer = new(
                sessionStore,
                sourceHasher,
                new SanitizedChangeDetector(),
                new ReverseMapper()
            );

            ImportResult importResult = importer.Import(sessionFilePath);

            Assert.False(importResult.RequiresReview);
            Assert.Single(importResult.Files);

            ImportedFileResult importedFile = importResult.Files[0];

            Assert.False(importedFile.RequiresReview);
            Assert.NotEmpty(importedFile.Patches);

            PatchApplicationService applicationService = new(
                new PatchApplier(),
                new PatchValidator(),
                sourceHasher
            );

            PatchApplicationResult applicationResult = applicationService.Apply(
                originalFilePath,
                session.Files[0].OriginalSourceHash,
                importedFile.Patches
            );

            Assert.True(applicationResult.Applied);
            Assert.False(applicationResult.RequiresReview);

            string finalSource = File.ReadAllText(originalFilePath);

            // Every real value must be restored independently.
            Assert.Contains("private string password = \"abc123\";", finalSource);

            Assert.Contains("private string apiKey = \"real-api-key\";", finalSource);

            Assert.Contains("private string secret = \"super-secret\";", finalSource);

            // The legitimate LLM change must survive.
            Assert.Contains("Use(password, apiKey, secret.ToUpper());", finalSource);

            // No dummy values may leak into real source.
            Assert.DoesNotContain("DUMMY_PASSWORD", finalSource);
            Assert.DoesNotContain("DUMMY_APIKEY", finalSource);
            Assert.DoesNotContain("DUMMY_SECRET", finalSource);
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
