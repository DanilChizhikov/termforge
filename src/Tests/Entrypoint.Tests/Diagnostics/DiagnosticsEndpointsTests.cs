using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DTech.Core.Connections;
using DTech.Core.Diagnostics;
using DTech.Entrypoint.SavedConnections;
using DTech.Entrypoint.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;

namespace DTech.Entrypoint.Tests.Diagnostics;

[TestClass]
public sealed class DiagnosticsEndpointsTests
{
    private const string ConnectionsPath = "/api/connections";

    private IRemoteCommandRunnerFactory _runnerFactory = null!;
    private TermForgeApplicationFactory _factory = null!;
    private HttpClient _client = null!;
    private AuthenticatedSession _session = null!;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public async Task Initialize()
    {
        _runnerFactory = Substitute.For<IRemoteCommandRunnerFactory>();
        var runner = Substitute.For<IRemoteCommandRunner>();
        _runnerFactory.ConnectAsync(default!, default).ReturnsForAnyArgs(SshResult<IRemoteCommandRunner>.Success(runner));
        _factory = new TermForgeApplicationFactory { ConfigureServices = ReplaceServices };
        _client = _factory.CreateClientWithoutCookies();
        _session = await SessionBootstrapper.AuthenticateAsync(_factory, _client, TestContext.CancellationToken);
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [TestMethod]
    public async Task Get_WithoutSession_IsUnauthorized()
    {
        using var anonymous = _factory.CreateClientWithoutCookies();
        var path = $"{ConnectionsPath}/{Guid.NewGuid()}/capabilities";

        using var response = await TestRequest.Get(path).SendAsync(anonymous, TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [TestMethod]
    public async Task Get_ReturnsReportAndCachesIt()
    {
        var id = await CreateConnectionAsync();
        var path = $"{ConnectionsPath}/{id}/capabilities";

        var first = await GetJsonAsync(path);
        var second = await GetJsonAsync(path);

        first.GetProperty("state").GetString().Should().Be("Completed");
        var capability = first.GetProperty("capabilities")[0];
        capability.GetProperty("id").GetString().Should().Be("stub");
        capability.GetProperty("status").GetString().Should().Be("Unavailable");
        capability.GetProperty("reason").GetString().Should().Be("stub reason");
        second.GetProperty("checkedAt").GetString().Should().Be(first.GetProperty("checkedAt").GetString());
        await _runnerFactory.ReceivedWithAnyArgs(1).ConnectAsync(default!, default);
    }

    [TestMethod]
    public async Task Refresh_RunsProbesAgain()
    {
        var id = await CreateConnectionAsync();
        await GetJsonAsync($"{ConnectionsPath}/{id}/capabilities");

        var refreshed = await PostJsonAsync($"{ConnectionsPath}/{id}/capabilities/refresh");

        refreshed.GetProperty("state").GetString().Should().Be("Completed");
        await _runnerFactory.ReceivedWithAnyArgs(2).ConnectAsync(default!, default);
    }

    [TestMethod]
    public async Task Get_OfUnknownConnection_IsNotFound()
    {
        var path = $"{ConnectionsPath}/{Guid.NewGuid()}/capabilities";

        using var response = await TestRequest.Get(path).WithSession(_session).SendAsync(_client, TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private void ReplaceServices(IServiceCollection services)
    {
        services.RemoveAll<IRemoteCommandRunnerFactory>();
        services.AddSingleton(_runnerFactory);
        services.RemoveAll<ICapabilityProbe>();
        services.AddSingleton<ICapabilityProbe, StubProbe>();
    }

    private async Task<Guid> CreateConnectionAsync()
    {
        var request = new SavedConnectionRequest
        {
            Name = "web",
            Host = "example.org",
            Port = 22,
            UserName = "deploy",
            AuthMethod = "Agent",
        };
        var message = TestRequest.Post(ConnectionsPath)
            .WithOrigin(TermForgeApplicationFactory.ApplicationOrigin)
            .WithSession(_session)
            .WithCsrfToken(_session.CsrfToken)
            .WithJson(request);
        using var response = await message.SendAsync(_client, TestContext.CancellationToken);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.CancellationToken);
        return body.GetProperty("id").GetGuid();
    }

    private async Task<JsonElement> GetJsonAsync(string path)
    {
        using var response = await TestRequest.Get(path).WithSession(_session).SendAsync(_client, TestContext.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.CancellationToken);
    }

    private async Task<JsonElement> PostJsonAsync(string path)
    {
        var message = TestRequest.Post(path)
            .WithOrigin(TermForgeApplicationFactory.ApplicationOrigin)
            .WithSession(_session)
            .WithCsrfToken(_session.CsrfToken);
        using var response = await message.SendAsync(_client, TestContext.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.CancellationToken);
    }

    private sealed class StubProbe : ICapabilityProbe
    {
        public Task<IReadOnlyList<CapabilityResult>> ProbeAsync(CapabilityProbeContext context, CancellationToken cancellationToken)
        {
            var unavailable = CapabilityResult.Unavailable("stub", "Stub", "stub reason");
            IReadOnlyList<CapabilityResult> results = [unavailable];
            return Task.FromResult(results);
        }
    }
}
