using System.Buffers.Binary;
using DTech.Core.Credentials;

namespace DTech.Credentials.Vault;

public static class VaultFileFormat
{
    public const ushort CurrentVersion = 1;
    public const byte Pbkdf2HmacSha256 = 1;
    public const int SaltLength = 32;
    public const int VerifierLength = 32;
    public const int NonceLength = 12;
    public const int TagLength = 16;
    public const int VersionOffset = 4;
    public const int KdfAlgorithmOffset = 6;
    public const int IterationsOffset = 8;
    public const int SaltOffset = 12;
    public const int VerifierOffset = SaltOffset + SaltLength;
    public const int NonceOffset = VerifierOffset + VerifierLength;
    public const int CiphertextLengthOffset = NonceOffset + NonceLength;
    public const int HeaderLength = CiphertextLengthOffset + sizeof(int);

    private const int ReservedOffset = 7;

    public static ReadOnlySpan<byte> Magic => "TFVT"u8;

    public static byte[] CreateFile(VaultHeader header)
    {
        var file = new byte[HeaderLength + header.CiphertextLength + TagLength];
        var span = file.AsSpan();
        Magic.CopyTo(span);
        BinaryPrimitives.WriteUInt16BigEndian(span[VersionOffset..], CurrentVersion);
        span[KdfAlgorithmOffset] = Pbkdf2HmacSha256;
        span[ReservedOffset] = 0;
        BinaryPrimitives.WriteInt32BigEndian(span[IterationsOffset..], header.KdfParameters.Iterations);
        header.KdfParameters.Salt.CopyTo(span[SaltOffset..]);
        header.Verifier.CopyTo(span[VerifierOffset..]);
        header.Nonce.CopyTo(span[NonceOffset..]);
        BinaryPrimitives.WriteInt32BigEndian(span[CiphertextLengthOffset..], header.CiphertextLength);
        return file;
    }

    public static CredentialResult<VaultHeader> ReadHeader(ReadOnlySpan<byte> file)
    {
        if (file.Length < Magic.Length || !file[..Magic.Length].SequenceEqual(Magic))
        {
            return Corrupted("the file is not a termforge vault.");
        }

        if (file.Length < HeaderLength)
        {
            return Corrupted("the header is truncated.");
        }

        var version = BinaryPrimitives.ReadUInt16BigEndian(file[VersionOffset..]);
        if (version != CurrentVersion)
        {
            var versionError = CredentialErrors.UnsupportedVaultVersion(version);
            return CredentialResult<VaultHeader>.Failure(versionError);
        }

        if (file[KdfAlgorithmOffset] != Pbkdf2HmacSha256 || file[ReservedOffset] != 0)
        {
            return Corrupted("the key derivation settings are unknown.");
        }

        var iterations = BinaryPrimitives.ReadInt32BigEndian(file[IterationsOffset..]);
        if (iterations <= 0)
        {
            return Corrupted("the key derivation iteration count is invalid.");
        }

        var ciphertextLength = BinaryPrimitives.ReadInt32BigEndian(file[CiphertextLengthOffset..]);
        if (ciphertextLength < 0 || (long)HeaderLength + ciphertextLength + TagLength != file.Length)
        {
            return Corrupted("the file length does not match the header.");
        }

        var salt = file.Slice(SaltOffset, SaltLength).ToArray();
        var verifier = file.Slice(VerifierOffset, VerifierLength).ToArray();
        var nonce = file.Slice(NonceOffset, NonceLength).ToArray();
        var kdfParameters = new VaultKdfParameters(salt, iterations);
        var header = new VaultHeader(kdfParameters, verifier, nonce, ciphertextLength);
        return CredentialResult<VaultHeader>.Success(header);
    }

    private static CredentialResult<VaultHeader> Corrupted(string detail)
    {
        var error = CredentialErrors.VaultCorrupted(detail);
        return CredentialResult<VaultHeader>.Failure(error);
    }
}
