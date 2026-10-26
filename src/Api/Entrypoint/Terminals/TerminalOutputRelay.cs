using System.Net.WebSockets;

namespace DTech.Entrypoint.Terminals;

internal static class TerminalOutputRelay
{
    private const int ReadBufferSize = 16 * 1024;

    public static async Task<TerminalRelayOutcome> RunAsync(TerminalRelayContext context, CancellationToken cancellationToken)
    {
        var buffer = new byte[ReadBufferSize];
        try
        {
            var readLength = await context.Session.ReadAsync(buffer, cancellationToken);
            while (readLength > 0)
            {
                var output = buffer.AsMemory(0, readLength);
                await context.Writer.SendOutputAsync(output, cancellationToken);
                readLength = await context.Session.ReadAsync(buffer, cancellationToken);
            }

            return TerminalRelayOutcome.SessionEnded;
        }
        catch (Exception exception) when (exception is WebSocketException or OperationCanceledException or ObjectDisposedException)
        {
            return TerminalRelayOutcome.BrowserDisconnected;
        }
    }
}
