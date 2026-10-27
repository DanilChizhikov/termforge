using System.Security.Cryptography;
using DTech.Core.Credentials;
using DTech.Core.SettingsTransfer;
using DTech.Core.SettingsTransfer.Secrets;
using DTech.Credentials.Vault;
using Microsoft.Extensions.Options;

namespace DTech.Credentials.Export;

public sealed class SecretsArchiveCipher(IOptions<CredentialStoreOptions> options) : ISecretsArchiveCipher
{
    public const string ExportIdEntryKey = "export-id";

    private const int ExportIdLength = 16;

    public byte[] Encrypt(SecretsArchiveContent content, Secret password)
    {
        using var entries = new VaultEntries();
        var exportIdBytes = content.ExportId.ToByteArray();
        entries.Set(ExportIdEntryKey, exportIdBytes);
        for (var i = 0; i < content.Secrets.Count; i++)
        {
            var secret = content.Secrets[i];
            var key = ConnectionSecretKeys.For(secret.ConnectionId, secret.Kind);
            var value = secret.Value.AsSpan();
            entries.Set(key.Value, value);
        }

        var plaintext = VaultEntriesSerializer.Serialize(entries);
        try
        {
            var kdfParameters = VaultKeyDerivation.CreateParameters(options.Value.VaultKdfIterations);
            var passwordBytes = password.AsSpan();
            using var keys = VaultKeyDerivation.Derive(passwordBytes, kdfParameters);
            return VaultCipher.Encrypt(keys, plaintext);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    public SettingsTransferResult<SecretsArchiveContent> Decrypt(byte[] archive, Secret password)
    {
        if (archive.Length > SettingsTransferLimits.MaxSecretsArchiveBytes)
        {
            var tooLarge = SettingsTransferError.TooLarge("secrets archive", SettingsTransferLimits.MaxSecretsArchiveBytes);
            return SettingsTransferResult<SecretsArchiveContent>.Failure(tooLarge);
        }

        var header = VaultFileFormat.ReadHeader(archive);
        if (!header.IsSuccess)
        {
            return HeaderFailure(header.Error);
        }

        if (header.Value.KdfParameters.Iterations > SettingsTransferLimits.MaxKdfIterations)
        {
            return Damaged();
        }

        var passwordBytes = password.AsSpan();
        using var keys = VaultKeyDerivation.Derive(passwordBytes, header.Value.KdfParameters);
        if (!VaultCipher.IsPasswordCorrect(keys, header.Value))
        {
            var wrongPassword = SettingsTransferError.WrongPassword();
            return SettingsTransferResult<SecretsArchiveContent>.Failure(wrongPassword);
        }

        var decrypted = VaultCipher.Decrypt(keys, header.Value, archive);
        if (!decrypted.IsSuccess)
        {
            return Damaged();
        }

        var parsed = VaultEntriesSerializer.Deserialize(decrypted.Value);
        CryptographicOperations.ZeroMemory(decrypted.Value);
        if (!parsed.IsSuccess)
        {
            return Damaged();
        }

        using var entries = parsed.Value;
        return ReadContent(entries);
    }

    private static SettingsTransferResult<SecretsArchiveContent> ReadContent(VaultEntries entries)
    {
        var exportIdEntry = entries.Find(ExportIdEntryKey);
        if (exportIdEntry is null || exportIdEntry.Value.Length != ExportIdLength)
        {
            return Damaged();
        }

        var exportId = new Guid(exportIdEntry.Value);
        var secrets = new List<ArchivedSecret>();
        var items = entries.Items;
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (string.Equals(item.Key, ExportIdEntryKey, StringComparison.Ordinal))
            {
                continue;
            }

            var known = ConnectionSecretKeys.TryParse(item.Key, out var connectionId, out var kind);
            if (!known || item.Value.Length == 0 || item.Value.Length > SettingsTransferLimits.MaxSecretBytes)
            {
                DisposeAll(secrets);
                return Damaged();
            }

            var value = Secret.FromBytes(item.Value);
            var secret = new ArchivedSecret(connectionId, kind, value);
            secrets.Add(secret);
        }

        var content = new SecretsArchiveContent(exportId, secrets);
        return SettingsTransferResult<SecretsArchiveContent>.Success(content);
    }

    private static SettingsTransferResult<SecretsArchiveContent> HeaderFailure(CredentialError error)
    {
        if (error.Code != CredentialErrorCode.UnsupportedVaultVersion)
        {
            return Damaged();
        }

        var unsupported = new SettingsTransferError(SettingsTransferErrorCode.UnsupportedVersion, "The secrets archive was created by a newer TermForge. Update TermForge to import it.");
        return SettingsTransferResult<SecretsArchiveContent>.Failure(unsupported);
    }

    private static SettingsTransferResult<SecretsArchiveContent> Damaged()
    {
        var damaged = SettingsTransferError.ArchiveDamaged();
        return SettingsTransferResult<SecretsArchiveContent>.Failure(damaged);
    }

    private static void DisposeAll(List<ArchivedSecret> secrets)
    {
        for (var i = 0; i < secrets.Count; i++)
        {
            secrets[i].Value.Dispose();
        }
    }
}
