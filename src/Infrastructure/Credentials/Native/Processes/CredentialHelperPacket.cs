using System.Security.Cryptography;

namespace DTech.Credentials.Native.Processes;

public sealed class CredentialHelperPacket<T>(T metadata, byte[] secretBytes) : IDisposable
{
    public T Metadata { get; } = metadata;

    public byte[] SecretBytes { get; } = secretBytes;

    public void Dispose()
    {
        CryptographicOperations.ZeroMemory(SecretBytes);
    }
}
