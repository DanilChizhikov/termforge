using System.Security.Cryptography;
using System.Text;
using DTech.Core.Credentials;
using DTech.Core.SettingsTransfer;
using DTech.Core.SettingsTransfer.Package;
using DTech.Core.SettingsTransfer.Secrets;
using DTech.Credentials.Vault;
using Microsoft.Extensions.Options;

namespace DTech.Credentials.Export;

public sealed class SettingsPackageCipher(IOptions<CredentialStoreOptions> options) : ISettingsPackageCipher
{
    public const string ExportIdEntryKey = "export-id";
    public const string SettingsEntryKey = "settings";

    private const int ExportIdLength = 16;

    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public byte[] Encrypt(SettingsPackage package, Secret key)
    {
        using var entries = new VaultEntries();
        var exportIdBytes = package.Secrets.ExportId.ToByteArray();
        entries.Set(ExportIdEntryKey, exportIdBytes);
        var settingsBytes = StrictUtf8.GetBytes(package.SettingsJson);
        entries.Set(SettingsEntryKey, settingsBytes);
        var secrets = package.Secrets.Secrets;
        for (var i = 0; i < secrets.Count; i++)
        {
            var secret = secrets[i];
            var secretKey = ConnectionSecretKeys.For(secret.ConnectionId, secret.Kind);
            var value = secret.Value.AsSpan();
            entries.Set(secretKey.Value, value);
        }

        var plaintext = VaultEntriesSerializer.Serialize(entries);
        try
        {
            var kdfParameters = VaultKeyDerivation.CreateParameters(options.Value.VaultKdfIterations);
            var keyBytes = key.AsSpan();
            using var keys = VaultKeyDerivation.Derive(keyBytes, kdfParameters);
            return VaultCipher.Encrypt(keys, plaintext);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    public SettingsTransferResult<SettingsPackage> Decrypt(byte[] package, Secret key)
    {
        if (package.Length > SettingsTransferLimits.MaxPackageBytes)
        {
            var tooLarge = SettingsTransferError.TooLarge("settings file", SettingsTransferLimits.MaxPackageBytes);
            return SettingsTransferResult<SettingsPackage>.Failure(tooLarge);
        }

        var header = VaultFileFormat.ReadHeader(package);
        if (!header.IsSuccess)
        {
            return HeaderFailure(header.Error);
        }

        if (header.Value.KdfParameters.Iterations > SettingsTransferLimits.MaxKdfIterations)
        {
            return Damaged();
        }

        var keyBytes = key.AsSpan();
        using var keys = VaultKeyDerivation.Derive(keyBytes, header.Value.KdfParameters);
        if (!VaultCipher.IsPasswordCorrect(keys, header.Value))
        {
            var wrongKey = SettingsTransferError.WrongKey();
            return SettingsTransferResult<SettingsPackage>.Failure(wrongKey);
        }

        var decrypted = VaultCipher.Decrypt(keys, header.Value, package);
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
        return ReadPackage(entries);
    }

    private static SettingsTransferResult<SettingsPackage> ReadPackage(VaultEntries entries)
    {
        var exportIdEntry = entries.Find(ExportIdEntryKey);
        if (exportIdEntry is null || exportIdEntry.Value.Length != ExportIdLength)
        {
            return Damaged();
        }

        var settingsJson = ReadSettingsJson(entries);
        if (settingsJson is null)
        {
            return Damaged();
        }

        var exportId = new Guid(exportIdEntry.Value);
        var secrets = new List<ArchivedSecret>();
        var items = entries.Items;
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (string.Equals(item.Key, ExportIdEntryKey, StringComparison.Ordinal) || string.Equals(item.Key, SettingsEntryKey, StringComparison.Ordinal))
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
        var package = new SettingsPackage(settingsJson, content);
        return SettingsTransferResult<SettingsPackage>.Success(package);
    }

    private static string? ReadSettingsJson(VaultEntries entries)
    {
        var settingsEntry = entries.Find(SettingsEntryKey);
        if (settingsEntry is null || settingsEntry.Value.Length == 0)
        {
            return null;
        }

        try
        {
            return StrictUtf8.GetString(settingsEntry.Value);
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
    }

    private static SettingsTransferResult<SettingsPackage> HeaderFailure(CredentialError error)
    {
        if (error.Code != CredentialErrorCode.UnsupportedVaultVersion)
        {
            return Damaged();
        }

        var unsupported = new SettingsTransferError(SettingsTransferErrorCode.UnsupportedVersion, "The settings file was created by a newer TermForge. Update TermForge to import it.");
        return SettingsTransferResult<SettingsPackage>.Failure(unsupported);
    }

    private static SettingsTransferResult<SettingsPackage> Damaged()
    {
        var damaged = SettingsTransferError.FileDamaged();
        return SettingsTransferResult<SettingsPackage>.Failure(damaged);
    }

    private static void DisposeAll(List<ArchivedSecret> secrets)
    {
        for (var i = 0; i < secrets.Count; i++)
        {
            secrets[i].Value.Dispose();
        }
    }
}
