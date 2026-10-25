namespace DTech.Credentials.Vault;

public sealed record VaultHeader(VaultKdfParameters KdfParameters, byte[] Verifier, byte[] Nonce, int CiphertextLength);
