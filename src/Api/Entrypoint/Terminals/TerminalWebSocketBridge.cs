using System.Net.WebSockets;
using DTech.Core.CommandHistory;
using DTech.Core.Terminal.Sessions;

namespace DTech.Entrypoint.Terminals;

internal sealed class TerminalWebSocketBridge(TerminalSessionManager sessionManager, CommandCaptureSessionFactory captureFactory, ILogger<TerminalWebSocketBridge> logger)
{
    private static readonly TimeSpan CloseHandshakeTimeout = TimeSpan.FromSeconds(2);

    public async Task RunAsync(WebSocket webSocket, ManagedTerminalSession session, CancellationToken requestAborted)
    {
        using var writer = new TerminalSocketWriter(webSocket);
        using var relayStop = CancellationTokenSource.CreateLinkedTokenSource(requestAborted);
        var capture = captureFactory.Create(session.Descriptor.ConnectionId);
        var context = new TerminalRelayContext(webSocket, session, writer, capture);
        var connected = TerminalServerMessage.Connected();
        await TrySendAsync(writer, connected, relayStop.Token);

        var outputRelay = TerminalOutputRelay.RunAsync(context, relayStop.Token);
        var inputRelay = TerminalInputRelay.RunAsync(context, relayStop.Token);
        var firstFinished = await Task.WhenAny(outputRelay, inputRelay);
        var outcome = await firstFinished;
        var reason = ResolveReason(session, outcome);
        await sessionManager.ReleaseAsync(session, reason);
        relayStop.CancelAfter(CloseHandshakeTimeout);
        if (outcome == TerminalRelayOutcome.SessionEnded)
        {
            var disconnected = TerminalServerMessage.Disconnected(reason);
            await TrySendAsync(writer, disconnected, relayStop.Token);
        }

        await TryCloseAsync(writer, relayStop.Token);
        await Task.WhenAll(outputRelay, inputRelay);
        await capture.CompleteAsync(CancellationToken.None);
        var descriptor = session.Descriptor;
        logger.LogInformation($"Terminal {descriptor.SessionNumber} of {descriptor.ConnectionName} closed: {session.CloseReason}");
    }

    private static string ResolveReason(ManagedTerminalSession session, TerminalRelayOutcome outcome)
    {
        if (session.CloseReason is not null)
        {
            return session.CloseReason;
        }

        return outcome == TerminalRelayOutcome.SessionEnded ? TerminalCloseReasons.RemoteShellExited : TerminalCloseReasons.BrowserDisconnected;
    }

    private static async Task TrySendAsync(TerminalSocketWriter writer, TerminalServerMessage message, CancellationToken cancellationToken)
    {
        try
        {
            await writer.SendMessageAsync(message, cancellationToken);
        }
        catch (Exception exception) when (exception is WebSocketException or OperationCanceledException or ObjectDisposedException)
        {
        }
    }

    private static async Task TryCloseAsync(TerminalSocketWriter writer, CancellationToken cancellationToken)
    {
        try
        {
            await writer.CloseAsync(WebSocketCloseStatus.NormalClosure, "terminal closed", cancellationToken);
        }
        catch (Exception exception) when (exception is WebSocketException or OperationCanceledException or ObjectDisposedException)
        {
        }
    }
}
