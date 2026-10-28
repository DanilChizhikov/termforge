using System.Text;
using DTech.Core.Diagnostics;

namespace DTech.Core.Docker;

public sealed class ContainerLogStream : IDockerOutputStream
{
    private const int ReadBufferSize = 16 * 1024;

    private readonly IRemoteCommandStream _stream;
    private readonly Decoder _decoder = Encoding.UTF8.GetDecoder();
    private readonly byte[] _buffer = new byte[ReadBufferSize];
    private readonly char[] _characters = new char[ReadBufferSize + 4];
    private bool _completed;

    public ContainerLogStream(IRemoteCommandStream stream)
    {
        _stream = stream;
    }

    public int? ExitCode => _stream.ExitCode;

    public async ValueTask<string?> ReadAsync(CancellationToken cancellationToken)
    {
        while (!_completed)
        {
            var read = await _stream.ReadAsync(_buffer, cancellationToken);
            var isEnd = read <= 0;
            var characterCount = _decoder.GetChars(_buffer, 0, Math.Max(read, 0), _characters, 0, isEnd);
            _completed = isEnd;
            if (characterCount > 0)
            {
                return new string(_characters, 0, characterCount);
            }
        }

        return null;
    }

    public ValueTask DisposeAsync()
    {
        return _stream.DisposeAsync();
    }
}
