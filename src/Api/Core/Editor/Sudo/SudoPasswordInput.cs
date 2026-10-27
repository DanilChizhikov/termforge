using System.Security.Cryptography;
using DTech.Core.Credentials;

namespace DTech.Core.Editor.Sudo;

public sealed class SudoPasswordInput : IDisposable
{
    private const byte LineFeed = (byte)'\n';

    private readonly byte[] _line;

    private SudoPasswordInput(byte[] line)
    {
        _line = line;
    }

    public ReadOnlyMemory<byte> Line => _line;

    public static SudoPasswordInput From(Secret password)
    {
        var bytes = password.AsSpan();
        var line = new byte[bytes.Length + 1];
        bytes.CopyTo(line);
        line[^1] = LineFeed;
        return new SudoPasswordInput(line);
    }

    public void Dispose()
    {
        CryptographicOperations.ZeroMemory(_line);
    }

    public override string ToString()
    {
        return Secret.Redacted;
    }
}
