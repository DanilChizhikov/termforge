namespace DTech.Credentials;

public sealed class CredentialStoreOptions
{
    public const string SectionName = "Credentials";
    public const int DefaultVaultKdfIterations = 600_000;

    public string? VaultPath { get; set; }

    public int VaultKdfIterations { get; set; } = DefaultVaultKdfIterations;

    public TimeSpan ProbeTimeout { get; set; } = TimeSpan.FromSeconds(10);
}
