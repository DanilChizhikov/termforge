using System.Net.WebSockets;
using DTech.Core.CommandHistory;
using DTech.Core.Connections;
using DTech.Core.Terminal;
using DTech.Core.Terminal.Sessions;
using DTech.Entrypoint.Terminals;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace DTech.Entrypoint.Tests.Terminals;

[TestClass]
public sealed class TerminalBridgeFailureTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task UnexpectedRelayOrInitialSendFailure_ReleasesSessionAndStopsTheOtherRelay(bool failSend)
    {
        var transport = Substitute.For<ITerminalSession>();
        transport.IsOpen.Returns(true);
        transport.ReadAsync(default, default).ReturnsForAnyArgs(_ => ValueTask.FromException<int>(new InvalidOperationException("read failed")));
        var factory = Substitute.For<ITerminalSessionFactory>();
        var success = SshResult<ITerminalSession>.Success(transport);
        factory.OpenAsync(default!, default).ReturnsForAnyArgs(success);
        await using var manager = new TerminalSessionManager(factory, TimeProvider.System, TerminalSessionOptions.Default);
        var endpoint = new SshEndpoint("test", 22);
        var authentication = new AgentAuthentication(null);
        var target = new SshConnectionTarget(endpoint, "test", authentication);
        var server = new TerminalServer(Guid.NewGuid(), "test", target);
        var size = new TerminalSize(80, 24);
        var request = new TerminalOpenRequest("owner", server, size);
        var opened = await manager.OpenAsync(request, TestContext.CancellationToken);
        var session = opened.Value;
        session.TryAttach().Should().BeTrue();
        var recorder = Substitute.For<ICommandHistoryRecorder>();
        var captures = new CommandCaptureSessionFactory(recorder, TimeProvider.System);
        var bridge = new TerminalWebSocketBridge(manager, captures, NullLogger<TerminalWebSocketBridge>.Instance);
        using var socket = new RelayWebSocket { FailSend = failSend };

        var run = async () => await bridge.RunAsync(socket, session, TestContext.CancellationToken);
        await run.Should().ThrowAsync<InvalidOperationException>();

        manager.Count.Should().Be(0);
        session.IsClosed.Should().BeTrue();
        await transport.Received(1).DisposeAsync();
        if (!failSend)
        {
            socket.ReceiveStopped.Task.IsCompleted.Should().BeTrue();
        }
    }

    [TestMethod]
    public async Task StalledWebSocketSend_IsAbortedWithinItsDeadline()
    {
        using var socket = new RelayWebSocket { StallSend = true };
        using var writer = new TerminalSocketWriter(socket);
        var message = TerminalServerMessage.Connected();
        var sending = writer.SendMessageAsync(message, TestContext.CancellationToken);

        var send = async () => await sending.WaitAsync(TimeSpan.FromSeconds(15), TestContext.CancellationToken);
        await send.Should().ThrowAsync<OperationCanceledException>();

        socket.AbortCount.Should().Be(1);
    }

    private sealed class RelayWebSocket : WebSocket
    {
        private WebSocketState _state = WebSocketState.Open;

        public bool FailSend { get; init; }
        public bool StallSend { get; init; }
        public int AbortCount { get; private set; }
        public TaskCompletionSource ReceiveStopped { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override WebSocketState State => _state;
        public override WebSocketCloseStatus? CloseStatus => null;
        public override string? CloseStatusDescription => null;
        public override string? SubProtocol => null;

        public override void Abort()
        {
            AbortCount++;
            _state = WebSocketState.Aborted;
        }

        public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
        {
            _state = WebSocketState.Closed;
            return Task.CompletedTask;
        }

        public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
        {
            _state = WebSocketState.CloseSent;
            return Task.CompletedTask;
        }

        public override void Dispose()
        {
            _state = WebSocketState.Closed;
        }

        public override async Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return new WebSocketReceiveResult(0, WebSocketMessageType.Close, true);
            }
            finally
            {
                ReceiveStopped.TrySetResult();
            }
        }

        public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
        {
            if (FailSend)
            {
                var failure = new InvalidOperationException("send failed");
                return Task.FromException(failure);
            }

            return StallSend ? Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken) : Task.CompletedTask;
        }
    }
}
