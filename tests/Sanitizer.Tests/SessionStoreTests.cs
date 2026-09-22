using Aegis.Pir.Enums;
using Aegis.Sanitizer;
using Aegis.Sanitizer.Models.Session;

namespace Sanitizer.Tests;

public class SessionStoreTests
{
    [Fact]
    public void SaveAndLoad_PreservesSession()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "aegis-tests",
            Guid.NewGuid().ToString()
        );

        string sessionPath = Path.Combine(directory, "session.json");

        try
        {
            AegisSession original = new()
            {
                Version = 1,
                SessionId = "test-session",
                Files =
                [
                    new SessionFile
                    {
                        OriginalFilePath = @"D:\project\Program.cs",

                        SanitizedFilePath = "sanitized/Program.cs",

                        OriginalSourceHash = "original-hash",

                        SanitizedSourceHash = "sanitized-hash",

                        BaselineSanitizedFilePath = baselinePath,

                        Mappings =
                        [
                            new SessionMapping
                            {
                                NodeId = "node-1",
                                NodeType = PirNodeType.Field,

                                OriginalText = "\"abc123\"",

                                DummyText = "\"DUMMY_PASSWORD\"",

                                OriginalStart = 42,
                                OriginalLength = 8,

                                SanitizedStart = 42,
                                SanitizedLength = 17,
                            },
                        ],
                    },
                ],
            };

            SessionStore store = new();

            store.Save(original, sessionPath);

            AegisSession loaded = store.Load(sessionPath);

            Assert.Equal(original.Version, loaded.Version);

            Assert.Equal(original.SessionId, loaded.SessionId);

            Assert.Single(loaded.Files);

            SessionFile file = loaded.Files[0];

            Assert.Equal(original.Files[0].OriginalFilePath, file.OriginalFilePath);

            Assert.Equal(original.Files[0].SanitizedFilePath, file.SanitizedFilePath);

            Assert.Equal("original-hash", file.OriginalSourceHash);

            Assert.Equal("sanitized-hash", file.SanitizedSourceHash);

            Assert.Single(file.Mappings);

            SessionMapping mapping = file.Mappings[0];

            Assert.Equal("node-1", mapping.NodeId);

            Assert.Equal(PirNodeType.Field, mapping.NodeType);

            Assert.Equal("\"abc123\"", mapping.OriginalText);

            Assert.Equal("\"DUMMY_PASSWORD\"", mapping.DummyText);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
