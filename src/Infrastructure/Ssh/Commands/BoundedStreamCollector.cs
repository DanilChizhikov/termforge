using System.Text;

namespace DTech.Ssh.Commands;

internal sealed class BoundedStreamCollector
{
    private const int ReadBufferSize = 4096;

    private readonly Stream _source;
    private readonly int _maxBytes;
    private readonly Action _onOverflow;
    private readonly MemoryStream _collected = new();

    public BoundedStreamCollector(Stream source, int maxBytes, Action onOverflow)
    {
        _source = source;
        _maxBytes = maxBytes;
        _onOverflow = onOverflow;
    }

    public bool Overflowed { get; private set; }

    public Task CollectAsync()
    {
        return Task.Run(Collect);
    }

    public string GetText()
    {
        var bytes = _collected.ToArray();
        return Encoding.UTF8.GetString(bytes);
    }

    public byte[] GetBytes()
    {
        return _collected.ToArray();
    }

    private void Collect()
    {
        var buffer = new byte[ReadBufferSize];
        try
        {
            while (true)
            {
                var read = _source.Read(buffer, 0, buffer.Length);
                if (read <= 0)
                {
                    return;
                }

                var remaining = _maxBytes - (int)_collected.Length;
                var accepted = Math.Min(read, remaining);
                _collected.Write(buffer, 0, accepted);
                if (accepted < read)
                {
                    Overflowed = true;
                    _onOverflow();
                    return;
                }
            }
        }
        catch (Exception exception) when (exception is ObjectDisposedException or IOException or InvalidOperationException)
        {
        }
    }
}
