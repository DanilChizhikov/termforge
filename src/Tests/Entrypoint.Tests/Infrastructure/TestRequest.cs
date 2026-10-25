using System.Net.Http.Json;

namespace DTech.Entrypoint.Tests.Infrastructure;

internal sealed class TestRequest
{
    private readonly HttpRequestMessage _message;

    private TestRequest(HttpMethod method, string path)
    {
        _message = new HttpRequestMessage(method, path);
    }

    public static TestRequest Get(string path)
    {
        return new TestRequest(HttpMethod.Get, path);
    }

    public static TestRequest Post(string path)
    {
        return new TestRequest(HttpMethod.Post, path);
    }

    public TestRequest WithOrigin(string origin)
    {
        _message.Headers.Add("Origin", origin);
        return this;
    }

    public TestRequest WithFetchSite(string fetchSite)
    {
        _message.Headers.Add("Sec-Fetch-Site", fetchSite);
        return this;
    }

    public TestRequest WithSession(AuthenticatedSession session)
    {
        _message.Headers.Add("Cookie", session.CookieHeader);
        return this;
    }

    public TestRequest WithCookie(string cookieHeader)
    {
        _message.Headers.Add("Cookie", cookieHeader);
        return this;
    }

    public TestRequest WithCsrfToken(string csrfToken)
    {
        _message.Headers.Add("X-CSRF-Token", csrfToken);
        return this;
    }

    public TestRequest WithJson<TValue>(TValue value)
    {
        var content = JsonContent.Create(value);
        _message.Content = content;
        return this;
    }

    public Task<HttpResponseMessage> SendAsync(HttpClient client, CancellationToken cancellationToken)
    {
        return client.SendAsync(_message, cancellationToken);
    }
}
