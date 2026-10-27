using DTech.Core.Credentials;
using DTech.Core.SettingsTransfer.Document;
using DTech.Core.SettingsTransfer.Secrets;

namespace DTech.Core.SettingsTransfer.Import;

public sealed class SettingsImportLoader(ISecretsArchiveCipher cipher)
{
    public SettingsTransferResult<LoadedSettingsImport> Load(SettingsImportRequest request)
    {
        var read = SettingsDocumentReader.Read(request.SettingsFile);
        if (!read.IsSuccess)
        {
            return SettingsTransferResult<LoadedSettingsImport>.Failure(read.Error);
        }

        var document = read.Value;
        var invalid = SettingsDocumentValidator.Validate(document);
        if (invalid is not null)
        {
            return SettingsTransferResult<LoadedSettingsImport>.Failure(invalid);
        }

        if (request.Secrets is null)
        {
            var withoutSecrets = new LoadedSettingsImport(document, null);
            return SettingsTransferResult<LoadedSettingsImport>.Success(withoutSecrets);
        }

        var secrets = OpenArchive(request.Secrets, document);
        if (!secrets.IsSuccess)
        {
            return SettingsTransferResult<LoadedSettingsImport>.Failure(secrets.Error);
        }

        var loaded = new LoadedSettingsImport(document, secrets.Value);
        return SettingsTransferResult<LoadedSettingsImport>.Success(loaded);
    }

    private SettingsTransferResult<SecretsArchiveContent> OpenArchive(SecretsImportInput input, SettingsDocument document)
    {
        if (input.Archive.Length > SettingsTransferLimits.MaxSecretsArchiveBytes)
        {
            var tooLarge = SettingsTransferError.TooLarge("secrets archive", SettingsTransferLimits.MaxSecretsArchiveBytes);
            return SettingsTransferResult<SecretsArchiveContent>.Failure(tooLarge);
        }

        if (input.Password.Length == 0 || input.Password.Length > SettingsTransferLimits.MaxArchivePasswordLength)
        {
            var wrongPassword = SettingsTransferError.WrongPassword();
            return SettingsTransferResult<SecretsArchiveContent>.Failure(wrongPassword);
        }

        using var password = Secret.FromString(input.Password);
        var decrypted = cipher.Decrypt(input.Archive, password);
        if (!decrypted.IsSuccess)
        {
            return decrypted;
        }

        var content = decrypted.Value;
        if (!BelongsTo(content, document))
        {
            content.Dispose();
            var mismatch = SettingsTransferError.ArchiveMismatch();
            return SettingsTransferResult<SecretsArchiveContent>.Failure(mismatch);
        }

        return decrypted;
    }

    private static bool BelongsTo(SecretsArchiveContent content, SettingsDocument document)
    {
        if (content.ExportId != document.ExportId)
        {
            return false;
        }

        var connectionIds = new HashSet<Guid>();
        var connections = document.Connections ?? [];
        for (var i = 0; i < connections.Count; i++)
        {
            connectionIds.Add(connections[i].Id);
        }

        for (var i = 0; i < content.Secrets.Count; i++)
        {
            if (!connectionIds.Contains(content.Secrets[i].ConnectionId))
            {
                return false;
            }
        }

        return true;
    }
}
