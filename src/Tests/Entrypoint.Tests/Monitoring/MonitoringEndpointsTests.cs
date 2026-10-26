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

namespace DTech.Entrypoint.Tests.Monitoring;

[TestClass]
public sealed class MonitoringEndpointsTests
{
    private const string ConnectionsPath = "/api/connections";
    private const string LinuxOutput = "@@os\nLinux\n@@cpu1\ncpu  100 0 50 800 0 0 0 0\n@@cpu2\ncpu  110 0 55 835 0 0 0 0\n@@uptime\n3600.25 7000.00\n@@mem\nMemTotal: 1000 kB\nMemAvailable: 400 kB\n@@disk\nFilesystem 1024-blocks Used Available Capacity Mounted on\n/dev/sda1 1000 400 600 40% /\n";

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
        var output = new RemoteCommandResult(0, LinuxOutput, string.Empty);
        runner.RunAsync(default!, default).ReturnsForAnyArgs(SshResult<RemoteCommandResult>.Success(output));
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
        var path = $"{ConnectionsPath}/{Guid.NewGuid()}/metrics";

        using var response = await TestRequest.Get(path).SendAsync(anonymous, TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [TestMethod]
    public async Task Get_ReturnsMetricsWithValuesAndUnavailableReasons()
    {
        var id = await CreateConnectionAsync();
        var path = $"{ConnectionsPath}/{id}/metrics";

        using var response = await TestRequest.Get(path).WithSession(_session).SendAsync(_client, TestContext.CancellationToken);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.GetProperty("state").GetString().Should().Be("Connected");
        body.GetProperty("uptimeSeconds").GetProperty("value").GetDouble().Should().Be(3600);
        body.GetProperty("cpuPercent").GetProperty("available").GetBoolean().Should().BeTrue();
        body.GetProperty("memory").GetProperty("value").GetProperty("totalBytes").GetInt64().Should().Be(1000 * 1024);
        body.GetProperty("disk").GetProperty("value").GetProperty("mountPoint").GetString().Should().Be("/");
    }

    [TestMethod]
    public async Task Get_OfUnknownConnection_IsNotFound()
    {
        var path = $"{ConnectionsPath}/{Guid.NewGuid()}/metrics";

        using var response = await TestRequest.Get(path).WithSession(_session).SendAsync(_client, TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private void ReplaceServices(IServiceCollection services)
    {
        services.RemoveAll<IRemoteCommandRunnerFactory>();
        services.AddSingleton(_runnerFactory);
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
}
