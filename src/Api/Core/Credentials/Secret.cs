using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace DTech.Core.Credentials;

[DebuggerDisplay(Redacted)]
public sealed class Secret : IDisposable
{
    public const string Redacted = "[redacted]";

    private readonly byte[] _bytes;
    private bool _disposed;

    private Secret(byte[] bytes)
    {
        _bytes = bytes;
    }

    public int Length
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _bytes.Length;
        }
    }

    public static Secret FromBytes(ReadOnlySpan<byte> bytes)
    {
        var copy = bytes.ToArray();
        return new Secret(copy);
    }

    public static Secret FromString(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        return new Secret(bytes);
    }

    public ReadOnlySpan<byte> AsSpan()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _bytes;
    }

    public string RevealString()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return Encoding.UTF8.GetString(_bytes);
    }

    public bool ContentEquals(Secret other)
    {
        var bytes = AsSpan();
        var otherBytes = other.AsSpan();
        return CryptographicOperations.FixedTimeEquals(bytes, otherBytes);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        CryptographicOperations.ZeroMemory(_bytes);
        _disposed = true;
    }

    public override string ToString()
    {
        return Redacted;
    }
}
