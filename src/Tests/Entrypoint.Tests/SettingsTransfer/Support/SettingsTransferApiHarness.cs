using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DTech.Core.Credentials;
using DTech.Entrypoint.Tests.Infrastructure;
using DTech.Persistence.Database;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace DTech.Entrypoint.Tests.SettingsTransfer.Support;

internal sealed class SettingsTransferApiHarness : IAsyncDisposable
{
    private static readonly Dictionary<string, string?> FastKeyDerivation = new()
    {
        ["Credentials:VaultKdfIterations"] = "1000",
    };

    private readonly TestContext _testContext;
    private HttpClient _client = null!;
    private AuthenticatedSession _session = null!;

    private SettingsTransferApiHarness(TestContext testContext)
    {
        _testContext = testContext;
        Factory = new TermForgeApplicationFactory(FastKeyDerivation) { ConfigureServices = ReplaceServices };
    }

    public InMemoryCredentialStore CredentialStore { get; } = new();

    public TermForgeApplicationFactory Factory { get; }

    public CapturingLoggerProvider Logs => Factory.Logs;

    public string DatabaseDirectory
    {
        get
        {
            var options = Factory.Services.GetRequiredService<IOptions<PersistenceOptions>>();
            return Path.GetDirectoryName(options.Value.DatabasePath)!;
        }
    }

    public static async Task<SettingsTransferApiHarness> StartAsync(TestContext testContext)
    {
        var harness = new SettingsTransferApiHarness(testContext);
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
        var content = await response.Content.ReadAsStringAsync(_testContext.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.Created, content);
        using var body = JsonDocument.Parse(content);
        return body.RootElement.GetProperty("id").GetGuid();
    }

    public async Task<JsonElement> ListConnectionsAsync()
    {
        using var response = await SendAsync(HttpMethod.Get, "/api/connections", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await ReadJsonAsync(response);
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

    public string? FindSecret(CredentialKey key)
    {
        return CredentialStore.Secrets.TryGetValue(key.Value, out var value) ? value : null;
    }

    private void ReplaceServices(IServiceCollection services)
    {
        services.RemoveAll<ICredentialStore>();
        services.AddSingleton<ICredentialStore>(CredentialStore);
    }
}
