using System.Net.Http.Json;
using System.Text.Json;
using DTech.Core.Terminal;
using DTech.Entrypoint.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DTech.Entrypoint.Tests.Terminals.Support;

internal sealed class TerminalApiHarness : IAsyncDisposable
{
    private readonly TestContext _testContext;
    private HttpClient _client = null!;
    private AuthenticatedSession _session = null!;

    private TerminalApiHarness(TestContext testContext)
    {
        _testContext = testContext;
        SessionFactory = new ScriptedTerminalSessionFactory();
        Factory = new TermForgeApplicationFactory { ConfigureServices = ReplaceServices };
    }

    public ScriptedTerminalSessionFactory SessionFactory { get; }

    public TermForgeApplicationFactory Factory { get; }

    public static async Task<TerminalApiHarness> StartAsync(TestContext testContext)
    {
        var harness = new TerminalApiHarness(testContext);
        harness._client = harness.Factory.CreateClientWithoutCookies();
        harness._session = await SessionBootstrapper.AuthenticateAsync(harness.Factory, harness._client, testContext.CancellationToken);
        return harness;
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await Factory.DisposeAsync();
    }

    public async Task<Guid> CreateConnectionAsync(string name, object? extra = null)
    {
        var request = new Dictionary<string, object?>
        {
            ["name"] = name,
            ["host"] = $"{name}.example.test",
            ["port"] = 22,
            ["userName"] = "deploy",
            ["authMethod"] = "Agent",
        };
        if (extra is not null)
        {
            var json = JsonSerializer.SerializeToElement(extra);
            foreach (var property in json.EnumerateObject())
            {
                request[property.Name] = property.Value;
            }
        }

        using var response = await SendAsync(HttpMethod.Post, "/api/connections", request);
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Created, await response.Content.ReadAsStringAsync(_testContext.CancellationToken));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(_testContext.CancellationToken);
        return body.GetProperty("id").GetGuid();
    }

    public async Task<JsonElement> OpenTerminalAsync(Guid connectionId)
    {
        using var response = await SendAsync(HttpMethod.Post, "/api/terminals", new { connectionId, columns = 80, rows = 24 });
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Created);
        return await response.Content.ReadFromJsonAsync<JsonElement>(_testContext.CancellationToken);
    }

    public async Task<TerminalSocket> AttachAsync(string id)
    {
        var client = Factory.Server.CreateWebSocketClient();
        client.ConfigureRequest = request => ApplyHeaders(request);
        var uri = new Uri($"ws://localhost/api/terminals/{id}/ws");
        var webSocket = await client.ConnectAsync(uri, _testContext.CancellationToken);
        return new TerminalSocket(webSocket);
    }

    public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Add("Origin", TermForgeApplicationFactory.ApplicationOrigin);
        request.Headers.Add("Cookie", _session.CookieHeader);
        request.Headers.Add("X-CSRF-Token", _session.CsrfToken);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await _client.SendAsync(request, _testContext.CancellationToken);
    }

    public async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        return await response.Content.ReadFromJsonAsync<JsonElement>(_testContext.CancellationToken);
    }

    private void ReplaceServices(IServiceCollection services)
    {
        services.RemoveAll<ITerminalSessionFactory>();
        services.AddSingleton<ITerminalSessionFactory>(SessionFactory);
    }

    private void ApplyHeaders(HttpRequest request)
    {
        request.Headers.Origin = TermForgeApplicationFactory.ApplicationOrigin;
        request.Headers.Cookie = _session.CookieHeader;
    }
}
