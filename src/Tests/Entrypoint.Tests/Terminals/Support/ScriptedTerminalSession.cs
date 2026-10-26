using System.Collections.Concurrent;
using System.Text;
using System.Threading.Channels;
using DTech.Core.Connections;
using DTech.Core.Terminal;

namespace DTech.Entrypoint.Tests.Terminals.Support;

internal sealed class ScriptedTerminalSession : ITerminalSession
{
    private readonly Channel<byte[]> _output = Channel.CreateUnbounded<byte[]>();
    private readonly ConcurrentQueue<TerminalSize> _resizes = new();
    private readonly StringBuilder _input = new();
    private readonly Lock _inputGate = new();
    private readonly TaskCompletionSource _disposed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private ReadOnlyMemory<byte> _pending;

    public bool IsOpen => !_disposed.Task.IsCompleted && !_output.Reader.Completion.IsCompleted;

    public bool IsDisposed => _disposed.Task.IsCompleted;

    public Task Disposed => _disposed.Task;

    public IReadOnlyCollection<TerminalSize> Resizes => _resizes;

    public string Input
    {
        get
        {
            lock (_inputGate)
            {
                return _input.ToString();
            }
        }
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
        if (!IsOpen)
        {
            var closed = new SshError(SshErrorKind.SessionClosed, "closed");
            return ValueTask.FromResult(SshResult.Failure(closed));
        }

        var text = Encoding.UTF8.GetString(data.Span);
        lock (_inputGate)
        {
            _input.Append(text);
        }

        Emit($"ECHO[{text}]");
        return ValueTask.FromResult(SshResult.Success());
    }

    public SshResult Resize(TerminalSize size)
    {
        if (!size.IsValid)
        {
            var invalid = new SshError(SshErrorKind.OperationFailed, $"Terminal size {size.Columns}x{size.Rows} is invalid");
            return SshResult.Failure(invalid);
        }

        _resizes.Enqueue(size);
        Emit($"SIZE[{size.Rows} {size.Columns}]");
        return SshResult.Success();
    }

    public void Emit(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        _output.Writer.TryWrite(bytes);
    }

    public void EndRemotely()
    {
        _output.Writer.TryComplete();
    }

    public ValueTask DisposeAsync()
    {
        _output.Writer.TryComplete();
        _disposed.TrySetResult();
        return ValueTask.CompletedTask;
    }
}
