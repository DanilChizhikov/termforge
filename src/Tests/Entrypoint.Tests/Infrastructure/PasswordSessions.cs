using System.Net.Http.Json;

namespace DTech.Entrypoint.Tests.Infrastructure;

internal static class PasswordSessions
{
    private const string SessionCookiePrefix = "termforge_session=";

    public static Task<HttpResponseMessage> SendLoginAsync(HttpClient client, LoginAttempt attempt, CancellationToken cancellationToken)
    {
        var loginBody = new LoginBody(attempt.Password);
        return TestRequest.Post(NetworkAccessSettings.LoginPath)
            .WithOrigin(attempt.Origin)
            .WithJson(loginBody)
            .SendAsync(client, cancellationToken);
    }

    public static async Task<AuthenticatedSession> SignInAsync(HttpClient client, CancellationToken cancellationToken)
    {
        var attempt = new LoginAttempt(NetworkAccessSettings.NetworkOrigin, NetworkAccessSettings.Password);
        using var loginResponse = await SendLoginAsync(client, attempt, cancellationToken);
        loginResponse.EnsureSuccessStatusCode();
        var setCookieHeader = SessionBootstrapper.ReadSetCookieHeader(loginResponse);
        var cookiePair = setCookieHeader.Split(';')[0];
        var sessionToken = cookiePair[SessionCookiePrefix.Length..];

        using var sessionResponse = await TestRequest.Get(SessionBootstrapper.SessionPath)
            .WithCookie(cookiePair)
            .SendAsync(client, cancellationToken);
        sessionResponse.EnsureSuccessStatusCode();
        var sessionBody = await sessionResponse.Content.ReadFromJsonAsync<SessionBody>(cancellationToken);
        return new AuthenticatedSession(sessionToken, sessionBody!.CsrfToken);
    }

    private sealed record LoginBody(string Password);

    private sealed record SessionBody(string CsrfToken);
}
