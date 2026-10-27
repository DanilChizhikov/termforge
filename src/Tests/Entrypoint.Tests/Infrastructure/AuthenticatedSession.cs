namespace DTech.Entrypoint.Tests.Infrastructure;

internal sealed record AuthenticatedSession(string SessionToken, string CsrfToken)
{
    public string CookieHeader => $"termforge_session={SessionToken}";
}
