using System.Text.Json;
using System.Text.Json.Serialization;
using Aegis.Sanitizer.Models.Session;

namespace Aegis.Sanitizer;

public sealed class SessionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters =
        {
            new JsonStringEnumConverter()
        }
    };

    public void Save(
        AegisSession session,
        string sessionFilePath
    )
    {
        ArgumentNullException.ThrowIfNull(session);

        string? directory =
            Path.GetDirectoryName(sessionFilePath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string json =
            JsonSerializer.Serialize(
                session,
                JsonOptions
            );

        File.WriteAllText(
            sessionFilePath,
            json
        );
    }

    public AegisSession Load(
        string sessionFilePath
    )
    {
        if (!File.Exists(sessionFilePath))
        {
            throw new FileNotFoundException(
                "Aegis session file was not found.",
                sessionFilePath
            );
        }

        string json =
            File.ReadAllText(
                sessionFilePath
            );

        AegisSession? session =
            JsonSerializer.Deserialize<AegisSession>(
                json,
                JsonOptions
            );

        return session
            ?? throw new InvalidOperationException(
                "The Aegis session file is invalid."
            );
    }
}