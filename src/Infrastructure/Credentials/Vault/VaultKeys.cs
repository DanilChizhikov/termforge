using System.Security.Cryptography;

namespace DTech.Credentials.Vault;

public sealed class VaultKeys : IDisposable
{
    private readonly byte[] _encryptionKey;
    private readonly byte[] _verifier;

    public VaultKeys(VaultKdfParameters kdfParameters, byte[] encryptionKey, byte[] verifier)
    {
        KdfParameters = kdfParameters;
        _encryptionKey = encryptionKey;
        _verifier = verifier;
    }

    public VaultKdfParameters KdfParameters { get; }

    public ReadOnlySpan<byte> EncryptionKey => _encryptionKey;

    public ReadOnlySpan<byte> Verifier => _verifier;

    public bool Matches(VaultKdfParameters kdfParameters)
    {
        return KdfParameters.Iterations == kdfParameters.Iterations && KdfParameters.Salt.AsSpan().SequenceEqual(kdfParameters.Salt);
    }

    public void Dispose()
    {
        CryptographicOperations.ZeroMemory(_encryptionKey);
        CryptographicOperations.ZeroMemory(_verifier);
    }

    public override string ToString()
    {
        return nameof(VaultKeys);
    }
}
