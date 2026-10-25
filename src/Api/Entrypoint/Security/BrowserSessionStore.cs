namespace DTech.Entrypoint.Security;

internal sealed class BrowserSessionStore
{
    private BrowserSession? _activeSession;

    public BrowserSession Create()
    {
        var sessionToken = RandomSecret.Create();
        var csrfToken = RandomSecret.Create();
        var session = new BrowserSession(sessionToken, csrfToken);
        Volatile.Write(ref _activeSession, session);
        return session;
    }

    public BrowserSession? Find(string? sessionToken)
    {
        var activeSession = Volatile.Read(ref _activeSession);
        if (activeSession is null)
        {
            return null;
        }

        return SecretComparer.AreEqual(activeSession.SessionToken, sessionToken) ? activeSession : null;
    }
}
