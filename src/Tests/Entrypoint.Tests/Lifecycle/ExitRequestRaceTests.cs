using DTech.Core.Transfers;
using DTech.Entrypoint.Lifecycle;
using DTech.Entrypoint.Tests.Lifecycle.Support;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace DTech.Entrypoint.Tests.Lifecycle;

[TestClass]
public sealed class ExitRequestRaceTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task CancelBeforeResponseCompletion_DoesNotRunShutdown()
    {
        await using var harness = await LifecycleApiHarness.StartAsync(TestContext);
        var connection = await harness.CreateConnectionAsync("exit-race");
        await harness.StartDownloadAsync(connection, "/remote/pending");
        using var graph = CreateGraph(harness);
        var response = new DeferredResponse();
        var context = CreateContext(response);
        var request = new ExitRequest(true);
        graph.Handler.RequestExit(context, request);
        graph.Coordinator.TryCancelWaiting().Should().BeTrue();

        var queue = new ContinuationQueue();
        queue.Run(() => response.CompleteAsync().GetAwaiter().GetResult());

        graph.Coordinator.State.Should().Be(ApplicationExitState.Running);
        graph.Clock.TimerCount.Should().Be(0);
        graph.Lifetime.DidNotReceive().StopApplication();
    }

    [TestMethod]
    public async Task OldResponseCompletion_CannotStartANewerExitRequest()
    {
        await using var harness = await LifecycleApiHarness.StartAsync(TestContext);
        var connection = await harness.CreateConnectionAsync("exit-race");
        await harness.StartDownloadAsync(connection, "/remote/pending");
        using var graph = CreateGraph(harness);
        var oldResponse = new DeferredResponse();
        var oldContext = CreateContext(oldResponse);
        var request = new ExitRequest(true);
        graph.Handler.RequestExit(oldContext, request);
        graph.Coordinator.TryCancelWaiting().Should().BeTrue();
        var currentResponse = new DeferredResponse();
        var currentContext = CreateContext(currentResponse);
        graph.Handler.RequestExit(currentContext, request);

        await oldResponse.CompleteAsync();
        graph.Clock.TimerCount.Should().Be(0);
        graph.Lifetime.DidNotReceive().StopApplication();
        await currentResponse.CompleteAsync();
        await currentResponse.CompleteAsync();

        graph.Coordinator.State.Should().Be(ApplicationExitState.WaitingForTransfers);
        graph.Clock.TimerCount.Should().Be(1);
        graph.Coordinator.TryCancelWaiting().Should().BeTrue();
    }

    [TestMethod]
    public async Task StartWhileRunning_DoesNotRunShutdown()
    {
        await using var harness = await LifecycleApiHarness.StartAsync(TestContext);
        using var graph = CreateGraph(harness);
        var queue = new ContinuationQueue();

        queue.Run(graph.Coordinator.Start);

        graph.Coordinator.State.Should().Be(ApplicationExitState.Running);
        graph.Lifetime.DidNotReceive().StopApplication();
    }

    private static ExitGraph CreateGraph(LifecycleApiHarness harness)
    {
        var control = harness.Factory.Services.GetRequiredService<TransferControl>();
        return new ExitGraph(control);
    }

    private static DefaultHttpContext CreateContext(DeferredResponse response)
    {
        var context = new DefaultHttpContext();
        context.Features.Set<IHttpResponseFeature>(response);
        return context;
    }

    private sealed class ExitGraph : IDisposable
    {
        public ExitGraph(TransferControl control)
        {
            var gate = new WorkAdmissionGate();
            var lifecycle = new LifecycleOptions();
            var options = Options.Create(lifecycle);
            IShutdownStep[] steps = [];
            var shutdown = new GracefulShutdown(steps, options, NullLogger<GracefulShutdown>.Instance);
            var watcher = new ActiveTransferWatcher(control, Clock);
            Coordinator = new ApplicationExitCoordinator(gate, shutdown, watcher, Lifetime, NullLogger<ApplicationExitCoordinator>.Instance);
            Handler = new ApplicationExitHandler(Coordinator, NullLogger<ApplicationExitHandler>.Instance);
        }

        public TimerCountingClock Clock { get; } = new();
        public IHostApplicationLifetime Lifetime { get; } = Substitute.For<IHostApplicationLifetime>();
        public ApplicationExitCoordinator Coordinator { get; }
        public ApplicationExitHandler Handler { get; }

        public void Dispose()
        {
            Coordinator.TryCancelWaiting();
            Coordinator.Dispose();
        }
    }

    private sealed class DeferredResponse : HttpResponseFeature
    {
        private readonly List<(Func<object, Task> Callback, object State)> _callbacks = [];

        public override void OnCompleted(Func<object, Task> callback, object state)
        {
            _callbacks.Add((callback, state));
        }

        public async Task CompleteAsync()
        {
            for (var i = _callbacks.Count - 1; i >= 0; i--)
            {
                var callback = _callbacks[i];
                await callback.Callback(callback.State);
            }
        }
    }

    private sealed class TimerCountingClock : TimeProvider
    {
        public int TimerCount { get; private set; }

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            TimerCount++;
            return new Timer(callback, state, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }
    }

    private sealed class ContinuationQueue : SynchronizationContext
    {
        private readonly Queue<(SendOrPostCallback Callback, object? State)> _callbacks = new();

        public override void Post(SendOrPostCallback callback, object? state)
        {
            _callbacks.Enqueue((callback, state));
        }

        public void Run(Action action)
        {
            var previous = Current;
            SetSynchronizationContext(this);
            try
            {
                action();
                while (_callbacks.TryDequeue(out var callback))
                {
                    callback.Callback(callback.State);
                }
            }
            finally
            {
                SetSynchronizationContext(previous);
            }
        }
    }
}
