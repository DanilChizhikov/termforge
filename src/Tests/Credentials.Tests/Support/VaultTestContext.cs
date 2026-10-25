using DTech.Credentials.Vault;
using Microsoft.Extensions.Options;

namespace DTech.Credentials.Tests.Support;

public sealed class VaultTestContext : IDisposable
{
    public const int TestIterations = 1_000;
    public const string MasterPassword = "correct horse battery staple";

    private readonly TemporaryDirectory _directory = new();
    private readonly List<EncryptedVault> _vaults = [];

    public VaultTestContext()
    {
        VaultPath = _directory.Combine("credentials.vault");
        var storeOptions = new CredentialStoreOptions
        {
            VaultPath = VaultPath,
            VaultKdfIterations = TestIterations,
            ProbeTimeout = TimeSpan.FromSeconds(5),
        };
        Options = Microsoft.Extensions.Options.Options.Create(storeOptions);
    }

    public string VaultPath { get; }

    public string DirectoryPath => _directory.DirectoryPath;

    public IOptions<CredentialStoreOptions> Options { get; }

    public RecordingLogger<EncryptedVault> VaultLogger { get; } = new();

    public EncryptedVault CreateVault()
    {
        var file = new VaultFile(Options);
        var vault = new EncryptedVault(file, Options, VaultLogger);
        _vaults.Add(vault);
        return vault;
    }

    public byte[] ReadVaultBytes()
    {
        return File.ReadAllBytes(VaultPath);
    }

    public void WriteVaultBytes(byte[] bytes)
    {
        File.WriteAllBytes(VaultPath, bytes);
    }

    public void FlipBit(int offset)
    {
        var bytes = ReadVaultBytes();
        bytes[offset] ^= 0x01;
        WriteVaultBytes(bytes);
    }

    public void Dispose()
    {
        for (var i = 0; i < _vaults.Count; i++)
        {
            _vaults[i].Dispose();
        }

        _directory.Dispose();
    }
}
