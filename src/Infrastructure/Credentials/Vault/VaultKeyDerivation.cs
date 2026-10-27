using System.Security.Cryptography;

namespace DTech.Credentials.Vault;

public static class VaultKeyDerivation
{
    public const int KeyLength = 32;

    private static ReadOnlySpan<byte> EncryptionKeyInfo => "termforge vault v1 encryption key"u8;

    private static ReadOnlySpan<byte> VerifierInfo => "termforge vault v1 password verifier"u8;

    public static VaultKdfParameters CreateParameters(int iterations)
    {
        var salt = RandomNumberGenerator.GetBytes(VaultFileFormat.SaltLength);
        return new VaultKdfParameters(salt, iterations);
    }

    public static VaultKeys Derive(ReadOnlySpan<byte> masterPassword, VaultKdfParameters kdfParameters)
    {
        var masterKey = new byte[KeyLength];
        try
        {
            Rfc2898DeriveBytes.Pbkdf2(masterPassword, kdfParameters.Salt, masterKey, kdfParameters.Iterations, HashAlgorithmName.SHA256);
            var encryptionKey = new byte[KeyLength];
            HKDF.Expand(HashAlgorithmName.SHA256, masterKey, encryptionKey, EncryptionKeyInfo);
            var verifier = new byte[KeyLength];
            HKDF.Expand(HashAlgorithmName.SHA256, masterKey, verifier, VerifierInfo);
            return new VaultKeys(kdfParameters, encryptionKey, verifier);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(masterKey);
        }
    }
}
