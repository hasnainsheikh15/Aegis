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
        string root =
            Path.Combine(
                Path.GetTempPath(),
                $"aegis-roundtrip-{Guid.NewGuid():N}"
            );

        Directory.CreateDirectory(root);

        try
        {
            string originalFilePath =
                Path.Combine(root, "Program.cs");

            string sessionDirectory =
                Path.Combine(root, ".aegis", "sessions", "test-session");

            string sanitizedDirectory =
                Path.Combine(sessionDirectory, "sanitized");

            string baselineDirectory =
                Path.Combine(sessionDirectory, "baseline");

            Directory.CreateDirectory(sanitizedDirectory);
            Directory.CreateDirectory(baselineDirectory);

            string sanitizedFilePath =
                Path.Combine(sanitizedDirectory, "Program.cs");

            string baselineFilePath =
                Path.Combine(baselineDirectory, "Program.cs");

            string sessionFilePath =
                Path.Combine(sessionDirectory, "session.json");

            string originalSource =
                "public class AuthService\r\n" +
                "{\r\n" +
                "    private string password = \"abc123\";\r\n" +
                "\r\n" +
                "    public void Login()\r\n" +
                "    {\r\n" +
                "        string token = password;\r\n" +
                "    }\r\n" +
                "}\r\n";

            string sanitizedSource =
                "public class AuthService\r\n" +
                "{\r\n" +
                "    private string password = \"DUMMY_PASSWORD\";\r\n" +
                "\r\n" +
                "    public void Login()\r\n" +
                "    {\r\n" +
                "        string token = password;\r\n" +
                "    }\r\n" +
                "}\r\n";

            File.WriteAllText(
                originalFilePath,
                originalSource
            );

            File.WriteAllText(
                sanitizedFilePath,
                sanitizedSource
            );

            File.WriteAllText(
                baselineFilePath,
                sanitizedSource
            );

            int originalSecretStart =
                originalSource.IndexOf(
                    "\"abc123\"",
                    StringComparison.Ordinal
                );

            int sanitizedSecretStart =
                sanitizedSource.IndexOf(
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
                        OriginalSourceHash =
                            sourceHasher.Compute(originalSource),
                        SanitizedSourceHash =
                            sourceHasher.Compute(sanitizedSource),
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
                                SanitizedLength =
                                    "\"DUMMY_PASSWORD\"".Length
                            }
                        ]
                    }
                ]
            };

            SessionStore sessionStore = new();

            sessionStore.Save(
                session,
                sessionFilePath
            );

            string modifiedSanitizedSource =
                sanitizedSource.Replace(
                    "string token = password;",
                    "string token = Hash(password);"
                );

            File.WriteAllText(
                sanitizedFilePath,
                modifiedSanitizedSource
            );

            SessionImporter importer =
                new(
                    sessionStore,
                    sourceHasher,
                    new SanitizedChangeDetector(),
                    new ReverseMapper()
                );

            ImportResult importResult =
                importer.Import(
                    sessionFilePath
                );

            Assert.False(importResult.RequiresReview);
            Assert.Single(importResult.Files);

            ImportedFileResult importedFile =
                importResult.Files[0];

            Assert.False(importedFile.RequiresReview);
            Assert.NotEmpty(importedFile.Patches);

            PatchApplicationService applicationService =
                new(
                    new PatchApplier(),
                    new PatchValidator(),
                    sourceHasher
                );

            PatchApplicationResult applicationResult =
                applicationService.Apply(
                    originalFilePath,
                    session.Files[0].OriginalSourceHash,
                    importedFile.Patches
                );

            Assert.True(applicationResult.Applied);
            Assert.False(applicationResult.RequiresReview);

            string finalSource =
                File.ReadAllText(
                    originalFilePath
                );

            Assert.Contains(
                "private string password = \"abc123\";",
                finalSource
            );

            Assert.Contains(
                "string token = Hash(password);",
                finalSource
            );

            Assert.DoesNotContain(
                "DUMMY_PASSWORD",
                finalSource
            );
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(
                    root,
                    recursive: true
                );
            }
        }
    }
}