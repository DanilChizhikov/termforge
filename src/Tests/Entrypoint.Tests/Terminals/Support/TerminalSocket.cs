using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace DTech.Entrypoint.Tests.Terminals.Support;

internal sealed class TerminalSocket(WebSocket webSocket) : IDisposable
{
    private static readonly TimeSpan ReceiveTimeout = TimeSpan.FromSeconds(10);

    private readonly StringBuilder _output = new();

    public WebSocket WebSocket => webSocket;

    public string Output => _output.ToString();

    public Task SendInputAsync(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        return webSocket.SendAsync(bytes, WebSocketMessageType.Binary, true, CancellationToken.None);
    }

    public Task SendControlAsync(string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        return webSocket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
    }

    public async Task<string> ExpectOutputAsync(string expected)
    {
        while (!Output.Contains(expected, StringComparison.Ordinal))
        {
            var frame = await ReceiveFrameAsync();
            if (frame.MessageType == WebSocketMessageType.Close)
            {
                throw new InvalidOperationException($"Socket closed before '{expected}' appeared. Output: {Output}");
            }
        }

        return Output;
    }

    public async Task<JsonElement> ExpectMessageAsync(string type)
    {
        while (true)
        {
            var frame = await ReceiveFrameAsync();
            if (frame.MessageType == WebSocketMessageType.Close)
            {
                throw new InvalidOperationException($"Socket closed before a '{type}' message arrived. Output: {Output}");
            }

            if (frame.MessageType != WebSocketMessageType.Text)
            {
                continue;
            }

            using var document = JsonDocument.Parse(frame.Payload);
            var message = document.RootElement.Clone();
            if (message.GetProperty("type").GetString() == type)
            {
                return message;
            }
        }
    }

    public async Task ExpectCloseAsync()
    {
        var frame = await ReceiveFrameAsync();
        while (frame.MessageType != WebSocketMessageType.Close)
        {
            frame = await ReceiveFrameAsync();
        }
    }

    public void Dispose()
    {
        webSocket.Dispose();
    }

    private async Task<TerminalFrame> ReceiveFrameAsync()
    {
        using var timeout = new CancellationTokenSource(ReceiveTimeout);
        using var payload = new MemoryStream();
        var buffer = new byte[4096];
        WebSocketReceiveResult result;
        do
        {
            result = await webSocket.ReceiveAsync(buffer, timeout.Token);
            payload.Write(buffer, 0, result.Count);
        }
        while (!result.EndOfMessage);

        var bytes = payload.ToArray();
        if (result.MessageType == WebSocketMessageType.Binary)
        {
            var text = Encoding.UTF8.GetString(bytes);
            _output.Append(text);
        }

        return new TerminalFrame(result.MessageType, bytes);
    }

    private sealed record TerminalFrame(WebSocketMessageType MessageType, byte[] Payload);
}
