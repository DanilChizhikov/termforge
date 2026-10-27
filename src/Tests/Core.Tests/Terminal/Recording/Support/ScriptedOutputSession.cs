using System.Text;
using System.Threading.Channels;
using DTech.Core.Connections;
using DTech.Core.Terminal;

namespace DTech.Core.Tests.Terminal.Recording.Support;

internal sealed class ScriptedOutputSession : ITerminalSession
{
    private readonly Channel<byte[]> _output = Channel.CreateUnbounded<byte[]>();
    private ReadOnlyMemory<byte> _pending;

    public bool IsOpen => !_output.Reader.Completion.IsCompleted;

    public void Emit(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        Emit(bytes);
    }

    public void Emit(byte[] bytes)
    {
        _output.Writer.TryWrite(bytes);
    }

    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (_pending.IsEmpty)
        {
            var hasData = await _output.Reader.WaitToReadAsync(cancellationToken);
            if (!hasData || !_output.Reader.TryRead(out var chunk))
            {
                return 0;
            }

            _pending = chunk;
        }

        var length = Math.Min(buffer.Length, _pending.Length);
        _pending[..length].Span.CopyTo(buffer.Span);
        _pending = _pending[length..];
        return length;
    }

    public ValueTask<SshResult> WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        var success = SshResult.Success();
        return ValueTask.FromResult(success);
    }

    public SshResult Resize(TerminalSize size)
    {
        return SshResult.Success();
    }

    public ValueTask DisposeAsync()
    {
        _output.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }
}
