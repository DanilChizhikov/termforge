namespace DTech.Core.Terminal.Sessions;

public sealed class TerminalOutputTail(int capacity)
{
    public const int DefaultCapacity = 64 * 1024;

    private readonly byte[] _buffer = new byte[capacity];
    private readonly Lock _gate = new();
    private int _start;
    private int _length;

    public TerminalOutputTail() : this(DefaultCapacity)
    {
    }

    public void Append(ReadOnlySpan<byte> output)
    {
        if (output.Length >= _buffer.Length)
        {
            output = output[^_buffer.Length..];
        }

        lock (_gate)
        {
            for (var i = 0; i < output.Length; i++)
            {
                var position = (_start + _length) % _buffer.Length;
                _buffer[position] = output[i];
                if (_length < _buffer.Length)
                {
                    _length++;
                }
                else
                {
                    _start = (_start + 1) % _buffer.Length;
                }
            }
        }
    }

    public byte[] Snapshot()
    {
        lock (_gate)
        {
            var snapshot = new byte[_length];
            for (var i = 0; i < _length; i++)
            {
                snapshot[i] = _buffer[(_start + i) % _buffer.Length];
            }

            return snapshot;
        }
    }
}
