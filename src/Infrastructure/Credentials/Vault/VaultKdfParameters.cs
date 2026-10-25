namespace DTech.Credentials.Vault;

public sealed record VaultKdfParameters(byte[] Salt, int Iterations);
