using System.Net;
using DTech.Core.Operations;
using DTech.Entrypoint.Tests.Lifecycle.Support;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DTech.Entrypoint.Tests.Lifecycle;

[TestClass]
public sealed class ApplicationShutdownTests
{
    private LifecycleApiHarness _harness = null!;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public async Task Initialize()
    {
        var settings = new Dictionary<string, string?> { ["Transfers:MaxParallelTransfers"] = "1" };
        _harness = await LifecycleApiHarness.StartAsync(TestContext, settings);
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        await _harness.DisposeAsync();
    }

    [TestMethod]
    public async Task Attention_WithoutPendingWork_ReportsNothing()
    {
        var attention = await _harness.GetAttentionAsync();

        attention.GetProperty("state").GetString().Should().Be("running");
        attention.GetProperty("transfers").GetArrayLength().Should().Be(0);
        attention.GetProperty("unsavedFiles").GetArrayLength().Should().Be(0);
        attention.GetProperty("terminalSessions").GetArrayLength().Should().Be(0);
    }

    [TestMethod]
    public async Task Attention_ReportsActiveTransfersUnsavedFilesAndTerminalSessions()
    {
        var connectionId = await _harness.CreateConnectionAsync("web-01");
        await _harness.OpenTerminalAsync(connectionId);
        await _harness.SaveDraftAsync(connectionId, "/etc/nginx/site.conf");
        await _harness.StartDownloadAsync(connectionId, "/remote/dist.tar.gz");

        var attention = await _harness.GetAttentionAsync();

        var transfer = attention.GetProperty("transfers").EnumerateArray().Single();
        transfer.GetProperty("server").GetString().Should().Be("web-01");
        transfer.GetProperty("sourcePath").GetString().Should().Be("/remote/dist.tar.gz");
        transfer.GetProperty("status").GetString().Should().Be("running");
        var unsavedFile = attention.GetProperty("unsavedFiles").EnumerateArray().Single();
        unsavedFile.GetProperty("server").GetString().Should().Be("web-01");
        unsavedFile.GetProperty("path").GetString().Should().Be("/etc/nginx/site.conf");
        var terminal = attention.GetProperty("terminalSessions").EnumerateArray().Single();
        terminal.GetProperty("server").GetString().Should().Be("web-01");
        terminal.GetProperty("sessionNumber").GetInt32().Should().Be(1);
        terminal.TryGetProperty("id", out _).Should().BeFalse();
    }

    [TestMethod]
    public async Task Exit_WithActiveTransfer_JournalsItAsCancelledAndClosesSessions()
    {
        var connectionId = await _harness.CreateConnectionAsync("web-01");
        await _harness.OpenTerminalAsync(connectionId);
        await _harness.StartDownloadAsync(connectionId, "/remote/dist.tar.gz");

        using var response = await _harness.RequestExitAsync(false);
        await _harness.WaitForStoppingAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var accepted = await _harness.ReadJsonAsync(response);
        accepted.GetProperty("state").GetString().Should().Be("shuttingDown");
        _harness.JournaledTransferOutcomes.Should().ContainSingle().Which.Should().Be(OperationStatus.Cancelled);
        _harness.TerminalSessions.Sessions.Should().ContainSingle().Which.IsDisposed.Should().BeTrue();
        Directory.EnumerateFiles(_harness.WorkDirectory, "*.part").Should().BeEmpty();
    }

    [TestMethod]
    public async Task Exit_WaitingForTransfers_RejectsNewWorkAndStopsAfterTransfersFinish()
    {
        var connectionId = await _harness.CreateConnectionAsync("web-01");
        await _harness.StartDownloadAsync(connectionId, "/remote/dist.tar.gz");

        using var response = await _harness.RequestExitAsync(true);
        var accepted = await _harness.ReadJsonAsync(response);
        using var rejected = await _harness.SendAsync(HttpMethod.Post, "/api/connections", new { name = "late" });
        var attention = await _harness.GetAttentionAsync();
        _harness.TransferGate.SetResult();
        await _harness.WaitForStoppingAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        accepted.GetProperty("state").GetString().Should().Be("waitingForTransfers");
        rejected.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        var problem = await _harness.ReadJsonAsync(rejected);
        problem.GetProperty("code").GetString().Should().Be("ApplicationExiting");
        attention.GetProperty("state").GetString().Should().Be("waitingForTransfers");
        _harness.JournaledTransferOutcomes.Should().ContainSingle().Which.Should().Be(OperationStatus.Succeeded);
    }

    [TestMethod]
    public async Task CancelExit_WhileWaitingForTransfers_ReopensTheApplication()
    {
        var connectionId = await _harness.CreateConnectionAsync("web-01");
        await _harness.StartDownloadAsync(connectionId, "/remote/dist.tar.gz");
        using var exit = await _harness.RequestExitAsync(true);

        using var cancelled = await _harness.SendAsync(HttpMethod.Delete, LifecycleApiHarness.ExitPath, null);
        _harness.TransferGate.SetResult();
        var attention = await _harness.GetAttentionAsync();
        var secondConnection = await _harness.CreateConnectionAsync("web-02");

        cancelled.StatusCode.Should().Be(HttpStatusCode.NoContent);
        attention.GetProperty("state").GetString().Should().Be("running");
        secondConnection.Should().NotBeEmpty();
        _harness.Factory.ApplicationStopping.IsCancellationRequested.Should().BeFalse();
    }

    [TestMethod]
    public async Task CancelExit_WithoutPendingExit_IsConflict()
    {
        using var response = await _harness.SendAsync(HttpMethod.Delete, LifecycleApiHarness.ExitPath, null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [TestMethod]
    public async Task Exit_RequestedTwice_IsConflict()
    {
        var connectionId = await _harness.CreateConnectionAsync("web-01");
        await _harness.StartDownloadAsync(connectionId, "/remote/dist.tar.gz");
        using var first = await _harness.RequestExitAsync(true);

        using var second = await _harness.RequestExitAsync(true);

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [TestMethod]
    public async Task ExitNow_WhileWaitingForTransfers_CancelsThemAndStops()
    {
        var connectionId = await _harness.CreateConnectionAsync("web-01");
        await _harness.StartDownloadAsync(connectionId, "/remote/dist.tar.gz");
        using var waiting = await _harness.RequestExitAsync(true);

        using var exitNow = await _harness.RequestExitAsync(false);
        await _harness.WaitForStoppingAsync();

        exitNow.StatusCode.Should().Be(HttpStatusCode.Accepted);
        _harness.JournaledTransferOutcomes.Should().ContainSingle().Which.Should().Be(OperationStatus.Cancelled);
    }

    [TestMethod]
    public async Task HostStopSignal_RunsGracefulShutdown()
    {
        var connectionId = await _harness.CreateConnectionAsync("web-01");
        await _harness.OpenTerminalAsync(connectionId);
        await _harness.StartDownloadAsync(connectionId, "/remote/dist.tar.gz");
        var lifetime = _harness.Factory.Services.GetRequiredService<IHostApplicationLifetime>();

        lifetime.StopApplication();

        _harness.JournaledTransferOutcomes.Should().ContainSingle().Which.Should().Be(OperationStatus.Cancelled);
        _harness.TerminalSessions.Sessions.Should().ContainSingle().Which.IsDisposed.Should().BeTrue();
    }
}
