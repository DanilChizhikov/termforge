using System.Security.Cryptography;
using DTech.Core.Credentials;

namespace DTech.Credentials.Vault;

public static class VaultCipher
{
    public static byte[] Encrypt(VaultKeys keys, ReadOnlySpan<byte> plaintext)
    {
        var nonce = RandomNumberGenerator.GetBytes(VaultFileFormat.NonceLength);
        var verifier = keys.Verifier.ToArray();
        var header = new VaultHeader(keys.KdfParameters, verifier, nonce, plaintext.Length);
        var file = VaultFileFormat.CreateFile(header);
        var span = file.AsSpan();
        var associatedData = span[..VaultFileFormat.HeaderLength];
        var ciphertext = span.Slice(VaultFileFormat.HeaderLength, plaintext.Length);
        var tag = span[(VaultFileFormat.HeaderLength + plaintext.Length)..];
        using var aes = new AesGcm(keys.EncryptionKey, VaultFileFormat.TagLength);
        aes.Encrypt(nonce, plaintext, ciphertext, tag, associatedData);
        return file;
    }

    public static bool IsPasswordCorrect(VaultKeys keys, VaultHeader header)
    {
        return CryptographicOperations.FixedTimeEquals(keys.Verifier, header.Verifier);
    }

    public static CredentialResult<byte[]> Decrypt(VaultKeys keys, VaultHeader header, ReadOnlySpan<byte> file)
    {
        var associatedData = file[..VaultFileFormat.HeaderLength];
        var ciphertext = file.Slice(VaultFileFormat.HeaderLength, header.CiphertextLength);
        var tag = file[(VaultFileFormat.HeaderLength + header.CiphertextLength)..];
        var plaintext = new byte[header.CiphertextLength];
        try
        {
            using var aes = new AesGcm(keys.EncryptionKey, VaultFileFormat.TagLength);
            aes.Decrypt(header.Nonce, ciphertext, tag, plaintext, associatedData);
            return CredentialResult<byte[]>.Success(plaintext);
        }
        catch (AuthenticationTagMismatchException)
        {
            CryptographicOperations.ZeroMemory(plaintext);
            var error = CredentialErrors.VaultCorrupted("the integrity check failed.");
            return CredentialResult<byte[]>.Failure(error);
        }
    }
}
