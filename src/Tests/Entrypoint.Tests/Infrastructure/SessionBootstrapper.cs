using System.Net.Http.Json;

namespace DTech.Entrypoint.Tests.Infrastructure;

internal static class SessionBootstrapper
{
    public const string ExchangePath = "/api/session/bootstrap";
    public const string SessionPath = "/api/session";
    private const string SessionCookiePrefix = "termforge_session=";

    public static Task<HttpResponseMessage> ExchangeAsync(HttpClient client, string? token, CancellationToken cancellationToken)
    {
        var exchangeBody = new ExchangeBody(token);
        return TestRequest.Post(ExchangePath)
            .WithOrigin(TermForgeApplicationFactory.ApplicationOrigin)
            .WithJson(exchangeBody)
            .SendAsync(client, cancellationToken);
    }

    public static async Task<AuthenticatedSession> AuthenticateAsync(TermForgeApplicationFactory factory, HttpClient client, CancellationToken cancellationToken)
    {
        var bootstrapToken = factory.BootstrapTokenValue;
        using var exchangeResponse = await ExchangeAsync(client, bootstrapToken, cancellationToken);
        exchangeResponse.EnsureSuccessStatusCode();
        var sessionToken = ReadSessionToken(exchangeResponse);

        using var sessionResponse = await TestRequest.Get(SessionPath)
            .WithCookie($"{SessionCookiePrefix}{sessionToken}")
            .SendAsync(client, cancellationToken);
        sessionResponse.EnsureSuccessStatusCode();
        var sessionBody = await sessionResponse.Content.ReadFromJsonAsync<SessionBody>(cancellationToken);
        return new AuthenticatedSession(sessionToken, sessionBody!.CsrfToken);
    }

    public static string ReadSetCookieHeader(HttpResponseMessage response)
    {
        var setCookieHeaders = response.Headers.GetValues("Set-Cookie").ToList();
        return setCookieHeaders.Single(header => header.StartsWith(SessionCookiePrefix, StringComparison.Ordinal));
    }

    private static string ReadSessionToken(HttpResponseMessage response)
    {
        var setCookieHeader = ReadSetCookieHeader(response);
        var cookiePair = setCookieHeader.Split(';')[0];
        return cookiePair[SessionCookiePrefix.Length..];
    }

    private sealed record ExchangeBody(string? Token);

    private sealed record SessionBody(string CsrfToken);
}
