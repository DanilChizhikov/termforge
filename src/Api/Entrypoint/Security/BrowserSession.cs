namespace DTech.Entrypoint.Security;

internal sealed record BrowserSession(string SessionToken, string CsrfToken);
