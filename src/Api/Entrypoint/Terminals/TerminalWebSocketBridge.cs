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
        var capture = captureFactory.Create(session);
        var context = new TerminalRelayContext(webSocket, session, writer, capture);
        Task outputRelay = Task.CompletedTask;
        Task inputRelay = Task.CompletedTask;
        try
        {
            var connected = TerminalServerMessage.Connected();
            await TrySendAsync(writer, connected, relayStop.Token);

            var output = TerminalOutputRelay.RunAsync(context, relayStop.Token);
            var input = TerminalInputRelay.RunAsync(context, relayStop.Token);
            outputRelay = output;
            inputRelay = input;
            var firstFinished = await Task.WhenAny(output, input);
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
        }
        finally
        {
            try
            {
                var reason = session.CloseReason ?? TerminalCloseReasons.BrowserDisconnected;
                await sessionManager.ReleaseAsync(session, reason);
            }
            finally
            {
                await relayStop.CancelAsync();
                try
                {
                    await Task.WhenAll(outputRelay, inputRelay);
                }
                finally
                {
                    await capture.CompleteAsync(CancellationToken.None);
                }
            }
        }

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
