using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace DTech.Core.Connections;

public sealed record HostKey(string Algorithm, string Sha256Fingerprint)
{
    public const string Sha256FingerprintPrefix = "SHA256:";

    private const int LengthPrefixSize = 4;

    public static HostKey FromPublicKeyBlob(ReadOnlySpan<byte> publicKeyBlob)
    {
        var algorithm = ReadKeyType(publicKeyBlob);
        var digest = SHA256.HashData(publicKeyBlob);
        var encodedDigest = Convert.ToBase64String(digest).TrimEnd('=');
        return new HostKey(algorithm, $"{Sha256FingerprintPrefix}{encodedDigest}");
    }

    public override string ToString()
    {
        return $"{Algorithm} {Sha256Fingerprint}";
    }

    private static string ReadKeyType(ReadOnlySpan<byte> publicKeyBlob)
    {
        if (publicKeyBlob.Length < LengthPrefixSize)
        {
            throw new FormatException("Public key blob is too short.");
        }

        var typeLength = BinaryPrimitives.ReadUInt32BigEndian(publicKeyBlob);
        var availableLength = (uint)(publicKeyBlob.Length - LengthPrefixSize);
        if (typeLength == 0 || typeLength > availableLength)
        {
            throw new FormatException("Public key blob has an invalid key type length.");
        }

        var typeBytes = publicKeyBlob.Slice(LengthPrefixSize, (int)typeLength);
        return Encoding.ASCII.GetString(typeBytes);
    }
}
