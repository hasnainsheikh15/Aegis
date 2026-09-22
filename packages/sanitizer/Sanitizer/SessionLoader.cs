using Aegis.Sanitizer.Models.Session;

namespace Aegis.Sanitizer;

public sealed class SessionLoader
{
    private readonly SessionStore sessionStore;

    public SessionLoader(SessionStore sessionStore)
    {
        this.sessionStore = sessionStore;
    }

    public AegisSession Load(string sessionFilePath)
    {
        return sessionStore.Load(sessionFilePath);
    }
}