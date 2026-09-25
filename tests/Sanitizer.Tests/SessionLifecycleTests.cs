using Aegis.Sanitizer.Models.Session;

namespace Sanitizer.Tests;

public class SessionLifecycleTests
{
    [Fact]
    public void NewSession_StartsReady()
    {
        AegisSession session = new()
        {
            SessionId = "test-session",
            Files = [],
        };

        Assert.Equal(SessionStatus.Ready, session.Status);
    }

    [Fact]
    public void ConsumedSession_CanBeMarkedConsumed()
    {
        AegisSession session = new()
        {
            SessionId = "test-session",
            Files = [],
        };

        session.Status = SessionStatus.Consumed;

        Assert.Equal(SessionStatus.Consumed, session.Status);
    }
}