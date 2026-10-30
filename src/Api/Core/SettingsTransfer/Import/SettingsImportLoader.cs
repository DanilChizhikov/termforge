using DTech.Core.Credentials;
using DTech.Core.SettingsTransfer.Document;
using DTech.Core.SettingsTransfer.Package;
using DTech.Core.SettingsTransfer.Secrets;

namespace DTech.Core.SettingsTransfer.Import;

public sealed class SettingsImportLoader(ISettingsPackageCipher cipher)
{
    public SettingsTransferResult<LoadedSettingsImport> Load(SettingsImportRequest request)
    {
        var opened = Open(request);
        if (!opened.IsSuccess)
        {
            return SettingsTransferResult<LoadedSettingsImport>.Failure(opened.Error);
        }

        var package = opened.Value;
        var document = ReadDocument(package);
        if (!document.IsSuccess)
        {
            package.Dispose();
            return SettingsTransferResult<LoadedSettingsImport>.Failure(document.Error);
        }

        var loaded = new LoadedSettingsImport(document.Value, package.Secrets);
        return SettingsTransferResult<LoadedSettingsImport>.Success(loaded);
    }

    private SettingsTransferResult<SettingsPackage> Open(SettingsImportRequest request)
    {
        if (request.Package.Length > SettingsTransferLimits.MaxPackageBytes)
        {
            var tooLarge = SettingsTransferError.TooLarge("settings file", SettingsTransferLimits.MaxPackageBytes);
            return SettingsTransferResult<SettingsPackage>.Failure(tooLarge);
        }

        var normalizedKey = ExportKeyNormalizer.Normalize(request.Key);
        if (normalizedKey is null)
        {
            var wrongKey = SettingsTransferError.WrongKey();
            return SettingsTransferResult<SettingsPackage>.Failure(wrongKey);
        }

        using var key = Secret.FromString(normalizedKey);
        return cipher.Decrypt(request.Package, key);
    }

    private static SettingsTransferResult<SettingsDocument> ReadDocument(SettingsPackage package)
    {
        var read = SettingsDocumentReader.Read(package.SettingsJson);
        if (!read.IsSuccess)
        {
            return read;
        }

        var document = read.Value;
        var invalid = SettingsDocumentValidator.Validate(document);
        if (invalid is not null)
        {
            return SettingsTransferResult<SettingsDocument>.Failure(invalid);
        }

        if (!BelongsTo(package.Secrets, document))
        {
            var damaged = SettingsTransferError.FileDamaged();
            return SettingsTransferResult<SettingsDocument>.Failure(damaged);
        }

        return read;
    }

    private static bool BelongsTo(SecretsArchiveContent secrets, SettingsDocument document)
    {
        if (secrets.ExportId != document.ExportId)
        {
            return false;
        }

        var connectionIds = new HashSet<Guid>();
        var connections = document.Connections ?? [];
        for (var i = 0; i < connections.Count; i++)
        {
            connectionIds.Add(connections[i].Id);
        }

        for (var i = 0; i < secrets.Secrets.Count; i++)
        {
            if (!connectionIds.Contains(secrets.Secrets[i].ConnectionId))
            {
                return false;
            }
        }

        return true;
    }
}
