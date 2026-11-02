using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;

namespace DTech.Credentials.Native.Processes;

public static class CredentialHelperProtocol
{
    public const int MaximumSecretBytes = 1024 * 1024;
    private const int MaximumMetadataBytes = 4096;
    private const int HeaderBytes = 8;

    public static async Task WriteAsync<T>(Stream stream, CredentialHelperPacket<T> packet, CancellationToken cancellationToken)
    {
        var metadata = JsonSerializer.SerializeToUtf8Bytes(packet.Metadata);
        try
        {
            ValidateLengths(metadata.Length, packet.SecretBytes.Length);
            var header = new byte[HeaderBytes];
            BinaryPrimitives.WriteInt32LittleEndian(header, metadata.Length);
            BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(4), packet.SecretBytes.Length);
            await stream.WriteAsync(header, cancellationToken);
            await stream.WriteAsync(metadata, cancellationToken);
            await stream.WriteAsync(packet.SecretBytes, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(metadata);
        }
    }

    public static async Task<CredentialHelperPacket<T>> ReadAsync<T>(Stream stream, CancellationToken cancellationToken)
    {
        var header = new byte[HeaderBytes];
        await stream.ReadExactlyAsync(header, cancellationToken);
        var metadataLength = BinaryPrimitives.ReadInt32LittleEndian(header);
        var secretLength = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(4));
        ValidateLengths(metadataLength, secretLength);
        var metadata = new byte[metadataLength];
        var secret = new byte[secretLength];
        var transferred = false;
        try
        {
            await stream.ReadExactlyAsync(metadata, cancellationToken);
            var parsed = JsonSerializer.Deserialize<T>(metadata);
            if (parsed is null)
            {
                throw new InvalidDataException("The credential helper returned invalid metadata.");
            }

            await stream.ReadExactlyAsync(secret, cancellationToken);
            var packet = new CredentialHelperPacket<T>(parsed, secret);
            transferred = true;
            return packet;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(metadata);
            if (!transferred)
            {
                CryptographicOperations.ZeroMemory(secret);
            }
        }
    }

    private static void ValidateLengths(int metadata, int secret)
    {
        if (metadata is <= 0 or > MaximumMetadataBytes || secret is < 0 or > MaximumSecretBytes)
        {
            throw new InvalidDataException("The credential helper message exceeds its size limits.");
        }
    }
}
