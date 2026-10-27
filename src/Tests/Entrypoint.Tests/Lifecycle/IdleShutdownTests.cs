using DTech.Entrypoint.Lifecycle;
using DTech.Entrypoint.Tests.Infrastructure;
using DTech.Entrypoint.Tests.Lifecycle.Support;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;

namespace DTech.Entrypoint.Tests.Lifecycle;

[TestClass]
public sealed class IdleShutdownTests
{
    private static readonly TimeSpan ObservationPeriod = TimeSpan.FromSeconds(2);

    private readonly MovableTimeProvider _timeProvider = new(DateTimeOffset.UtcNow);
    private readonly IBrowserPresence _browserPresence = Substitute.For<IBrowserPresence>();

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task IdleShutdown_WithoutConnectedBrowser_StopsTheApplication()
    {
        _browserPresence.IsAnyBrowserConnected.Returns(false);
        await using var harness = await StartAsync("1");

        var stopped = await AdvanceUntilStoppedAsync(harness);

        stopped.Should().BeTrue();
    }

    [TestMethod]
    public async Task IdleShutdown_WithConnectedBrowser_KeepsRunning()
    {
        _browserPresence.IsAnyBrowserConnected.Returns(true);
        await using var harness = await StartAsync("1");

        var stopped = await AdvanceUntilStoppedAsync(harness);

        stopped.Should().BeFalse();
    }

    [TestMethod]
    public async Task IdleShutdown_WithActiveTransfer_KeepsRunning()
    {
        _browserPresence.IsAnyBrowserConnected.Returns(false);
        await using var harness = await StartAsync("1");
        var connectionId = await harness.CreateConnectionAsync("web-01");
        await harness.StartDownloadAsync(connectionId, "/remote/dist.tar.gz");

        var stopped = await AdvanceUntilStoppedAsync(harness);

        stopped.Should().BeFalse();
    }

    [TestMethod]
    public async Task IdleShutdown_DisabledByDefault_KeepsRunning()
    {
        _browserPresence.IsAnyBrowserConnected.Returns(false);
        await using var harness = await StartAsync(null);

        var stopped = await AdvanceUntilStoppedAsync(harness);

        stopped.Should().BeFalse();
    }

    private Task<LifecycleApiHarness> StartAsync(string? idleShutdownMinutes)
    {
        var settings = new Dictionary<string, string?>
        {
            ["Transfers:MaxParallelTransfers"] = "1",
            ["Lifecycle:IdleCheckIntervalMilliseconds"] = "20",
        };
        if (idleShutdownMinutes is not null)
        {
            settings["Lifecycle:IdleShutdownMinutes"] = idleShutdownMinutes;
        }

        return LifecycleApiHarness.StartAsync(TestContext, settings, ReplaceIdleDependencies);
    }

    private void ReplaceIdleDependencies(IServiceCollection services)
    {
        services.RemoveAll<TimeProvider>();
        services.AddSingleton<TimeProvider>(_timeProvider);
        services.RemoveAll<IBrowserPresence>();
        services.AddSingleton(_browserPresence);
    }

    private async Task<bool> AdvanceUntilStoppedAsync(LifecycleApiHarness harness)
    {
        var deadline = DateTime.UtcNow + ObservationPeriod;
        var stopping = harness.Factory.ApplicationStopping;
        while (DateTime.UtcNow < deadline && !stopping.IsCancellationRequested)
        {
            _timeProvider.Advance(TimeSpan.FromSeconds(20));
            await Task.Delay(50, TestContext.CancellationToken);
        }

        return stopping.IsCancellationRequested;
    }
}
