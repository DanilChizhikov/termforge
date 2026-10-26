using System.Net;
using System.Text;

namespace DTech.Entrypoint.Tests.Updates;

internal sealed class StubReleaseHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _respond;
    private int _requestCount;

    public StubReleaseHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond)
    {
        _respond = respond;
    }

    public int RequestCount => _requestCount;

    public HttpRequestMessage? LastRequest { get; private set; }

    public static StubReleaseHandler Json(string body)
    {
        return new StubReleaseHandler(_ => Task.FromResult(JsonResponse(HttpStatusCode.OK, body)));
    }

    public static StubReleaseHandler Status(HttpStatusCode statusCode)
    {
        return new StubReleaseHandler(_ => Task.FromResult(new HttpResponseMessage(statusCode)));
    }

    public static StubReleaseHandler Throwing()
    {
        return new StubReleaseHandler(_ => throw new HttpRequestException("network down"));
    }

    public static string ReleasesJson(params (string Tag, bool Draft, bool Prerelease)[] releases)
    {
        var items = new List<string>();
        for (var i = 0; i < releases.Length; i++)
        {
            var release = releases[i];
            var draft = release.Draft ? "true" : "false";
            var prerelease = release.Prerelease ? "true" : "false";
            var url = $"https://github.com/DanilChizhikov/termforge/releases/tag/{release.Tag}";
            items.Add($"{{\"tag_name\":\"{release.Tag}\",\"html_url\":\"{url}\",\"draft\":{draft},\"prerelease\":{prerelease}}}");
        }

        return $"[{string.Join(',', items)}]";
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _requestCount);
        LastRequest = request;
        return _respond(request);
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string body)
    {
        var content = new StringContent(body, Encoding.UTF8, "application/json");
        return new HttpResponseMessage(statusCode) { Content = content };
    }
}
