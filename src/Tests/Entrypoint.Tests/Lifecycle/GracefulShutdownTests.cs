using System.Collections.Concurrent;
using System.Diagnostics;
using DTech.Entrypoint.Lifecycle;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DTech.Entrypoint.Tests.Lifecycle;

[TestClass]
public sealed class GracefulShutdownTests
{
    private readonly ConcurrentQueue<ShutdownStage> _executed = new();

    [TestMethod]
    public async Task RunAsync_RunsStepsInStageOrder()
    {
        IShutdownStep[] steps =
        [
            new RecordingStep(ShutdownStage.FlushWorkspace, _executed),
            new RecordingStep(ShutdownStage.CloseSessions, _executed),
            new RecordingStep(ShutdownStage.StopAcceptingWork, _executed),
            new RecordingStep(ShutdownStage.FlushJournal, _executed),
            new RecordingStep(ShutdownStage.CancelTransfers, _executed),
        ];
        var shutdown = CreateShutdown(steps, 5);

        var outcome = await shutdown.RunAsync();

        outcome.Should().Be(ShutdownOutcome.Completed);
        _executed.Should().Equal(
            ShutdownStage.StopAcceptingWork,
            ShutdownStage.CancelTransfers,
            ShutdownStage.CloseSessions,
            ShutdownStage.FlushJournal,
            ShutdownStage.FlushWorkspace);
    }

    [TestMethod]
    public async Task RunAsync_CalledTwice_RunsStepsOnce()
    {
        IShutdownStep[] steps = [new RecordingStep(ShutdownStage.CloseSessions, _executed)];
        var shutdown = CreateShutdown(steps, 5);

        var first = shutdown.RunAsync();
        var second = shutdown.RunAsync();
        await Task.WhenAll(first, second);

        second.Should().BeSameAs(first);
        _executed.Should().ContainSingle();
    }

    [TestMethod]
    public async Task RunAsync_WhenStepHangs_StopsAtTimeout()
    {
        IShutdownStep[] steps =
        [
            new HangingStep(ShutdownStage.CancelTransfers),
            new RecordingStep(ShutdownStage.FlushWorkspace, _executed),
        ];
        var shutdown = CreateShutdown(steps, 1);
        var stopwatch = Stopwatch.StartNew();

        var outcome = await shutdown.RunAsync();

        outcome.Should().Be(ShutdownOutcome.TimedOut);
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5));
        _executed.Should().BeEmpty();
    }

    [TestMethod]
    public async Task RunAsync_WhenStepFails_ContinuesWithLaterSteps()
    {
        IShutdownStep[] steps =
        [
            new FailingStep(ShutdownStage.CloseSessions),
            new RecordingStep(ShutdownStage.FlushWorkspace, _executed),
        ];
        var shutdown = CreateShutdown(steps, 5);

        var outcome = await shutdown.RunAsync();

        outcome.Should().Be(ShutdownOutcome.Completed);
        _executed.Should().Equal(ShutdownStage.FlushWorkspace);
    }

    private static GracefulShutdown CreateShutdown(IShutdownStep[] steps, int timeoutSeconds)
    {
        var lifecycleOptions = new LifecycleOptions { ShutdownTimeoutSeconds = timeoutSeconds };
        var options = Options.Create(lifecycleOptions);
        return new GracefulShutdown(steps, options, NullLogger<GracefulShutdown>.Instance);
    }

    private sealed class RecordingStep(ShutdownStage stage, ConcurrentQueue<ShutdownStage> executed) : IShutdownStep
    {
        public ShutdownStage Stage => stage;

        public Task RunAsync(CancellationToken cancellationToken)
        {
            executed.Enqueue(stage);
            return Task.CompletedTask;
        }
    }

    private sealed class HangingStep(ShutdownStage stage) : IShutdownStep
    {
        public ShutdownStage Stage => stage;

        public Task RunAsync(CancellationToken cancellationToken)
        {
            var neverCompletes = new TaskCompletionSource();
            return neverCompletes.Task;
        }
    }

    private sealed class FailingStep(ShutdownStage stage) : IShutdownStep
    {
        public ShutdownStage Stage => stage;

        public Task RunAsync(CancellationToken cancellationToken)
        {
            var failure = new InvalidOperationException("step failed");
            return Task.FromException(failure);
        }
    }
}
