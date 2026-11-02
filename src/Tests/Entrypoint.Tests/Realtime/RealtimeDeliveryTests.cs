using System.Net.WebSockets;
using DTech.Entrypoint.Lifecycle;
using DTech.Entrypoint.Realtime;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace DTech.Entrypoint.Tests.Realtime;

[TestClass]
public sealed class RealtimeDeliveryTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Publishing_never_waits_for_a_stalled_socket_or_delays_a_healthy_client()
    {
        var channel = CreateChannel();
        using var slow = new TestRealtimeSocket(true);
        using var fast = new TestRealtimeSocket();
        await using var slowConnection = channel.Connect(slow, TestContext.CancellationToken);
        await using var fastConnection = channel.Connect(fast, TestContext.CancellationToken);

        var first = channel.Publish("item", 1, TestContext.CancellationToken);
        first.IsCompletedSuccessfully.Should().BeTrue();
        await slow.SendStarted.WaitAsync(TestTimeout, TestContext.CancellationToken);
        var firstMessage = await fast.ReadMessageAsync(TestContext.CancellationToken);
        var second = channel.Publish("item", 2, TestContext.CancellationToken);
        second.IsCompletedSuccessfully.Should().BeTrue();
        var secondMessage = await fast.ReadMessageAsync(TestContext.CancellationToken);

        firstMessage.Should().Be("{\"type\":\"item\",\"data\":1}");
        secondMessage.Should().Be("{\"type\":\"item\",\"data\":2}");
        slow.AbortCount.Should().Be(0);
        channel.ConnectionCount.Should().Be(2);
    }

    [TestMethod]
    public async Task Queue_overflow_aborts_only_the_slow_connection_and_removes_its_presence()
    {
        var channel = CreateChannel();
        using var slow = new TestRealtimeSocket(true);
        using var fast = new TestRealtimeSocket();
        await using var slowConnection = channel.Connect(slow, TestContext.CancellationToken);
        await using var fastConnection = channel.Connect(fast, TestContext.CancellationToken);
        var presence = new RealtimeBrowserPresence(channel);
        await channel.Publish("item", 0, TestContext.CancellationToken);
        await slow.SendStarted.WaitAsync(TestTimeout, TestContext.CancellationToken);
        await fast.ReadMessageAsync(TestContext.CancellationToken);

        for (var i = 1; i <= 64; i++)
        {
            var publish = channel.Publish("item", i, TestContext.CancellationToken);
            publish.IsCompletedSuccessfully.Should().BeTrue();
            await fast.ReadMessageAsync(TestContext.CancellationToken);
        }

        channel.ConnectionCount.Should().Be(2);
        await channel.Publish("item", 65, TestContext.CancellationToken);
        var last = await fast.ReadMessageAsync(TestContext.CancellationToken);
        await slow.AbortStarted.WaitAsync(TestTimeout, TestContext.CancellationToken);

        last.Should().Contain("65");
        slowConnection.Failure.Should().Be("outgoing queue overflow");
        slow.AbortCount.Should().Be(1);
        channel.ConnectionCount.Should().Be(1);
        presence.IsAnyBrowserConnected.Should().BeTrue();
        await fastConnection.DisposeAsync();
        presence.IsAnyBrowserConnected.Should().BeFalse();
    }

    [TestMethod]
    public async Task Send_deadline_aborts_an_uncancellable_send_and_observes_its_completion()
    {
        var clock = new RealtimeTestClock();
        var channel = CreateChannel(clock);
        using var socket = new TestRealtimeSocket(true);
        await using var connection = channel.Connect(socket, TestContext.CancellationToken);
        await channel.Publish("item", 1, TestContext.CancellationToken);
        await clock.TimerCreated.WaitAsync(TestTimeout, TestContext.CancellationToken);

        clock.Expire();
        var disposed = connection.DisposeAsync().AsTask();
        await disposed.WaitAsync(TestTimeout, TestContext.CancellationToken);

        clock.DueTime.Should().Be(TimeSpan.FromSeconds(10));
        connection.Failure.Should().Be("send timeout");
        socket.AbortCount.Should().Be(1);
        channel.ConnectionCount.Should().Be(0);
    }

    [TestMethod]
    public async Task Socket_send_failure_does_not_escape_to_publisher_and_removes_the_connection()
    {
        var channel = CreateChannel();
        var failure = new WebSocketException();
        using var socket = new TestRealtimeSocket { SendFailure = failure };
        await using var connection = channel.Connect(socket, TestContext.CancellationToken);

        var publish = channel.Publish("item", 1, TestContext.CancellationToken);
        publish.IsCompletedSuccessfully.Should().BeTrue();
        await socket.SendStarted.WaitAsync(TestTimeout, TestContext.CancellationToken);
        var disposed = connection.DisposeAsync().AsTask();
        await disposed.WaitAsync(TestTimeout, TestContext.CancellationToken);

        channel.ConnectionCount.Should().Be(0);
        socket.AbortCount.Should().Be(1);
    }

    [TestMethod]
    public async Task Concurrent_publishers_preserve_one_order_for_every_client_and_serialize_once()
    {
        var channel = CreateChannel();
        using var firstSocket = new TestRealtimeSocket();
        using var secondSocket = new TestRealtimeSocket();
        await using var first = channel.Connect(firstSocket, TestContext.CancellationToken);
        await using var second = channel.Connect(secondSocket, TestContext.CancellationToken);
        var payloads = new CountingPayload[32];
        var publishes = new Task[32];
        for (var i = 0; i < publishes.Length; i++)
        {
            var payload = new CountingPayload(i);
            payloads[i] = payload;
            publishes[i] = Task.Run(() => channel.Publish("item", payload, TestContext.CancellationToken), TestContext.CancellationToken);
        }

        await Task.WhenAll(publishes);
        var firstMessages = new List<string>();
        var secondMessages = new List<string>();
        for (var i = 0; i < publishes.Length; i++)
        {
            var firstMessage = await firstSocket.ReadMessageAsync(TestContext.CancellationToken);
            var secondMessage = await secondSocket.ReadMessageAsync(TestContext.CancellationToken);
            firstMessages.Add(firstMessage);
            secondMessages.Add(secondMessage);
            payloads[i].Reads.Should().Be(1);
        }

        firstMessages.Should().Equal(secondMessages);
        firstSocket.ConcurrentSends.Should().BeFalse();
        secondSocket.ConcurrentSends.Should().BeFalse();
    }

    [TestMethod]
    public async Task Cancellation_removes_an_idle_connection_and_does_not_dispose_active_callbacks()
    {
        var channel = CreateChannel();
        using var socket = new TestRealtimeSocket();
        using var stopping = new CancellationTokenSource();
        await using var connection = channel.Connect(socket, stopping.Token);
        using var callbackEntered = new ManualResetEventSlim();
        using var releaseCallback = new ManualResetEventSlim();
        using var registration = connection.Stopping.Register(() =>
        {
            callbackEntered.Set();
            releaseCallback.Wait();
        });

        stopping.Cancel();
        channel.ConnectionCount.Should().Be(0);
        var entered = Task.Run(() => callbackEntered.Wait(TestTimeout), TestContext.CancellationToken);
        (await entered).Should().BeTrue();
        var disposing = connection.DisposeAsync().AsTask();
        try
        {
            disposing.IsCompleted.Should().BeFalse();
        }
        finally
        {
            releaseCallback.Set();
        }

        await disposing.WaitAsync(TestTimeout, TestContext.CancellationToken);
        socket.AbortCount.Should().Be(1);
    }

    [TestMethod]
    public async Task Close_handshake_uses_the_single_sender_after_an_in_flight_message()
    {
        var channel = CreateChannel();
        using var socket = new TestRealtimeSocket(true);
        await using var connection = channel.Connect(socket, TestContext.CancellationToken);
        await channel.Publish("item", 1, TestContext.CancellationToken);
        await socket.SendStarted.WaitAsync(TestTimeout, TestContext.CancellationToken);

        connection.RequestClose();
        var disposing = connection.DisposeAsync().AsTask();
        disposing.IsCompleted.Should().BeFalse();
        socket.ReleaseSend();
        await disposing.WaitAsync(TestTimeout, TestContext.CancellationToken);

        socket.CloseCount.Should().Be(1);
        socket.ConcurrentSends.Should().BeFalse();
        channel.ConnectionCount.Should().Be(0);
    }

    [TestMethod]
    public async Task OverflowPublishing_DoesNotWaitForSocketAbortCallbacks()
    {
        var channel = CreateChannel();
        using var releaseAbort = new ManualResetEventSlim();
        using var socket = new TestRealtimeSocket(true) { AbortRelease = releaseAbort };
        await using var connection = channel.Connect(socket, TestContext.CancellationToken);
        await channel.Publish("item", 0, TestContext.CancellationToken);
        await socket.SendStarted.WaitAsync(TestTimeout, TestContext.CancellationToken);
        try
        {
            for (var i = 1; i <= 65; i++)
            {
                var published = channel.Publish("item", i, TestContext.CancellationToken);
                published.IsCompletedSuccessfully.Should().BeTrue();
            }

            await socket.AbortStarted.WaitAsync(TestTimeout, TestContext.CancellationToken);
            channel.ConnectionCount.Should().Be(0);
        }
        finally
        {
            releaseAbort.Set();
        }
    }

    private static RealtimeChannel CreateChannel(TimeProvider? clock = null)
    {
        var logger = NullLogger<RealtimeChannel>.Instance;
        var timeProvider = clock ?? TimeProvider.System;
        return new RealtimeChannel(logger, timeProvider);
    }

    private sealed class CountingPayload(int value)
    {
        private int _reads;

        [System.Text.Json.Serialization.JsonIgnore]
        public int Reads => Volatile.Read(ref _reads);

        public int Value
        {
            get
            {
                Interlocked.Increment(ref _reads);
                return value;
            }
        }
    }
}
