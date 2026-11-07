using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using DTech.Core.NetworkDiagnostics;
using DTech.Core.Safety;
using DTech.Core.SavedConnections;
using DTech.Entrypoint.NetworkDiagnostics;
using DTech.Entrypoint.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace DTech.Entrypoint.Tests.Diagnostics;

[TestClass]
public sealed class NetworkDiagnosticEndpointsTests
{
    private const string Prefix = "/api/network-diagnostics";
    private TermForgeApplicationFactory _factory = null!;
    private HttpClient _client = null!;
    private AuthenticatedSession _session = null!;
    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public async Task Initialize()
    {
        _factory = new TermForgeApplicationFactory();
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
    public async Task Start_WithoutSession_IsUnauthorized()
    {
        using var response = await TestRequest.Post(Prefix).WithJson(Request()).SendAsync(_client, TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [TestMethod]
    public async Task Start_WithoutCsrf_IsRejected()
    {
        using var response = await TestRequest.Post(Prefix).WithSession(_session).WithOrigin(TermForgeApplicationFactory.ApplicationOrigin)
            .WithJson(Request()).SendAsync(_client, TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [TestMethod]
    public async Task FullScan_RequiresExactOneTimeConfirmation()
    {
        var request = Request() with { AllPorts = true };
        using var refused = await PostAsync(Prefix, request);
        refused.StatusCode.Should().Be((HttpStatusCode)428);
        using var prepared = await PostAsync(Prefix + "/prepare", request);
        var plan = await prepared.Content.ReadFromJsonAsync<JsonElement>(TestContext.CancellationToken);
        var token = plan.GetProperty("confirmation").GetProperty("token").GetString();
        var changed = request with { Host = "localhost", ConfirmationToken = token };
        using var wrong = await PostAsync(Prefix, changed);
        wrong.StatusCode.Should().Be((HttpStatusCode)428);
    }

    [TestMethod]
    public async Task Run_ReportsPointAndResultsAndKeepsThemAfterCompletion()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var request = Request() with { Ports = port.ToString() };
        using var created = await PostAsync(Prefix, request);
        created.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var started = await created.Content.ReadFromJsonAsync<NetworkDiagnosticSnapshot>(TestContext.CancellationToken);
        var store = _factory.Services.GetRequiredService<NetworkDiagnosticStore>();
        var run = store.Find(started!.Id)!;
        await WaitAsync(() => run.Snapshot().Status == "completed");

        using var response = await GetAsync($"{Prefix}/{started.Id}/results");
        var results = await response.Content.ReadFromJsonAsync<NetworkDiagnosticResult[]>(TestContext.CancellationToken);

        results.Should().ContainSingle();
        results![0].Point.Category.Should().Be("rf");
        results[0].PointName.Should().Be("Local machine");
        results[0].Outcome.Status.Should().Be(NetworkProbeOutcome.Responded);
        using var retained = await GetAsync($"{Prefix}/{started.Id}");
        retained.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [TestMethod]
    public async Task Cancel_StopsPendingUdpProbeAndPreservesRun()
    {
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var port = ((IPEndPoint)server.Client.LocalEndPoint!).Port;
        var request = Request() with { Protocols = ["udp"], Ports = port.ToString() };
        using var created = await PostAsync(Prefix, request);
        var started = await created.Content.ReadFromJsonAsync<NetworkDiagnosticSnapshot>(TestContext.CancellationToken);
        using var cancelled = await PostAsync($"{Prefix}/{started!.Id}/cancel", new { });
        var store = _factory.Services.GetRequiredService<NetworkDiagnosticStore>();
        var run = store.Find(started.Id)!;

        await WaitAsync(() => run.Snapshot().Status == "cancelled");

        cancelled.StatusCode.Should().Be(HttpStatusCode.Accepted);
        run.Snapshot().Status.Should().Be("cancelled");
    }

    private static NetworkDiagnosticRequest Request()
    {
        return new NetworkDiagnosticRequest { Host = "127.0.0.1", Ports = "1", Protocols = ["tcp"], Points = [new NetworkDiagnosticPoint(null, "rf")] };
    }

    [TestMethod]
    public void Cancel_QueuedRun_UpdatesStateBeforeWorkerReadsIt()
    {
        using var store = new NetworkDiagnosticStore();
        var request = Request();
        var plan = NetworkDiagnosticPlanner.Create(request);
        var run = store.Create(plan);

        run.Cancel();

        run.Snapshot().Status.Should().Be("cancelled");
        run.TryStart().Should().BeFalse();
    }

    [TestMethod]
    public async Task FullScan_ValidConfirmation_CanOnlyQueueOnce()
    {
        using var store = new NetworkDiagnosticStore();
        var repository = Substitute.For<ISavedConnectionRepository>();
        var confirmations = new ConfirmationTokenStore(TimeProvider.System);
        var service = new NetworkDiagnosticService(repository, confirmations, store);
        var request = Request() with { AllPorts = true };
        var prepared = await service.PrepareAsync(request, TestContext.CancellationToken);
        var confirmed = request with { ConfirmationToken = prepared.Confirmation!.Token };

        var created = await service.StartAsync(confirmed, TestContext.CancellationToken);
        var again = () => service.StartAsync(confirmed, TestContext.CancellationToken);

        created.Total.Should().Be(65535);
        await again.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [TestMethod]
    public async Task FailedPoint_DoesNotStopLocalPoint()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var request = Request() with { Ports = port.ToString(), Points = [new NetworkDiagnosticPoint(null, "rf"), new NetworkDiagnosticPoint(Guid.NewGuid(), "outsideRf")] };
        var plan = NetworkDiagnosticPlanner.Create(request);
        var store = _factory.Services.GetRequiredService<NetworkDiagnosticStore>();
        var run = store.Create(plan);

        await WaitAsync(() => run.Snapshot().Status == "completed");

        var results = run.Page(0, 10);
        results.Should().HaveCount(2);
        results.Should().Contain(result => result.Point.ConnectionId == null && result.Outcome.Status == NetworkProbeOutcome.Responded);
        results.Should().Contain(result => result.Point.ConnectionId != null && result.Outcome.Status == NetworkProbeOutcome.Error);
    }
    private Task<HttpResponseMessage> PostAsync<T>(string path, T body)
    {
        return TestRequest.Post(path).WithSession(_session).WithCsrfToken(_session.CsrfToken)
            .WithOrigin(TermForgeApplicationFactory.ApplicationOrigin).WithJson(body).SendAsync(_client, TestContext.CancellationToken);
    }
    private Task<HttpResponseMessage> GetAsync(string path)
    {
        return TestRequest.Get(path).WithSession(_session).SendAsync(_client, TestContext.CancellationToken);
    }
    private async Task WaitAsync(Func<bool> condition)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        while (!condition()) await Task.Delay(20, timeout.Token);
    }
}
