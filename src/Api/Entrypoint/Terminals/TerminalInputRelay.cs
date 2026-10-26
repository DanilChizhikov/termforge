using System.Buffers;
using System.Net.WebSockets;
using DTech.Core.Terminal;

namespace DTech.Entrypoint.Terminals;

internal static class TerminalInputRelay
{
    public const int MaxControlMessageSize = 4 * 1024;

    private const int ReceiveBufferSize = 16 * 1024;
    private const string UnsupportedControlMessage = "Unsupported terminal control message.";

    public static async Task<TerminalRelayOutcome> RunAsync(TerminalRelayContext context, CancellationToken cancellationToken)
    {
        var buffer = new byte[ReceiveBufferSize];
        var controlMessage = new ArrayBufferWriter<byte>(MaxControlMessageSize);
        try
        {
            while (context.WebSocket.State == WebSocketState.Open)
            {
                var received = await context.WebSocket.ReceiveAsync(buffer.AsMemory(), cancellationToken);
                if (received.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }

                var payload = buffer.AsMemory(0, received.Count);
                if (received.MessageType == WebSocketMessageType.Binary)
                {
                    await context.Session.WriteAsync(payload, cancellationToken);
                    continue;
                }

                if (controlMessage.WrittenCount + received.Count > MaxControlMessageSize)
                {
                    await context.Writer.CloseAsync(WebSocketCloseStatus.MessageTooBig, "control message too large", cancellationToken);
                    break;
                }

                controlMessage.Write(payload.Span);
                if (received.EndOfMessage)
                {
                    await ApplyControlMessageAsync(context, controlMessage.WrittenMemory, cancellationToken);
                    controlMessage.ResetWrittenCount();
                }
            }
        }
        catch (Exception exception) when (exception is WebSocketException or OperationCanceledException or ObjectDisposedException)
        {
        }

        return TerminalRelayOutcome.BrowserDisconnected;
    }

    private static async Task ApplyControlMessageAsync(TerminalRelayContext context, ReadOnlyMemory<byte> json, CancellationToken cancellationToken)
    {
        var message = TerminalControlMessage.Parse(json.Span);
        if (message is null || !message.IsResize)
        {
            var unsupported = TerminalServerMessage.Error(UnsupportedControlMessage);
            await context.Writer.SendMessageAsync(unsupported, cancellationToken);
            return;
        }

        var size = new TerminalSize(message.Columns, message.Rows);
        var resized = context.Session.Resize(size);
        if (!resized.IsSuccess)
        {
            var failure = TerminalServerMessage.Error(resized.Error!.Message);
            await context.Writer.SendMessageAsync(failure, cancellationToken);
        }
    }
}
