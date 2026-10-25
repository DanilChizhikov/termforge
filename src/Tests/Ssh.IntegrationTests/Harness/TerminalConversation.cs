using System.Text;
using System.Text.RegularExpressions;
using DTech.Core.Terminal;

namespace DTech.Ssh.IntegrationTests.Harness;

internal sealed class TerminalConversation(ITerminalSession session)
{
    private static readonly TimeSpan ResponseTimeout = TimeSpan.FromSeconds(20);

    private readonly StringBuilder _transcript = new();
    private int _consumedLength;

    public string Transcript => _transcript.ToString();

    public async Task SendLineAsync(string line)
    {
        var bytes = Encoding.UTF8.GetBytes($"{line}\n");
        var result = await session.WriteAsync(bytes, CancellationToken.None);
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException($"Terminal write failed: {result.Error}");
        }
    }

    public async Task<Match> ExpectAsync(Regex pattern)
    {
        using var timeout = new CancellationTokenSource(ResponseTimeout);
        var buffer = new byte[4096];
        var decoder = Encoding.UTF8.GetDecoder();
        var characters = new char[Encoding.UTF8.GetMaxCharCount(buffer.Length)];
        while (true)
        {
            var unreadText = Transcript[_consumedLength..];
            var match = pattern.Match(unreadText);
            if (match.Success)
            {
                _consumedLength += match.Index + match.Length;
                return match;
            }

            var readLength = await ReadAsync(buffer, timeout.Token);
            if (readLength == 0)
            {
                throw new InvalidOperationException($"Terminal closed before '{pattern}' appeared. Transcript: {Transcript}");
            }

            var characterCount = decoder.GetChars(buffer, 0, readLength, characters, 0);
            _transcript.Append(characters, 0, characterCount);
        }
    }

    public async Task WaitForCloseAsync()
    {
        using var timeout = new CancellationTokenSource(ResponseTimeout);
        var buffer = new byte[4096];
        var readLength = await ReadAsync(buffer, timeout.Token);
        while (readLength > 0)
        {
            readLength = await ReadAsync(buffer, timeout.Token);
        }
    }

    private async Task<int> ReadAsync(byte[] buffer, CancellationToken cancellationToken)
    {
        try
        {
            return await session.ReadAsync(buffer, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException($"No expected terminal output within {ResponseTimeout}. Transcript: {Transcript}");
        }
    }
}
