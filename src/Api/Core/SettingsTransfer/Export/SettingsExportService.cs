using System.Text.Json;
using DTech.Core.Credentials;
using DTech.Core.SettingsTransfer.Document;

namespace DTech.Core.SettingsTransfer.Export;

public sealed class SettingsExportService(SettingsDocumentBuilder documentBuilder, SecretsArchiveBuilder archiveBuilder, TimeProvider timeProvider)
{
    public async Task<SettingsTransferResult<SettingsExport>> ExportAsync(SettingsExportRequest request, CancellationToken cancellationToken)
    {
        var invalid = Validate(request);
        if (invalid is not null)
        {
            return SettingsTransferResult<SettingsExport>.Failure(invalid);
        }

        var exportedAt = timeProvider.GetUtcNow().UtcDateTime;
        var exportId = Guid.NewGuid();
        var header = new SettingsDocumentHeader(exportId, exportedAt, request.Secrets is not null);
        var document = await documentBuilder.BuildAsync(header, request.Sections, cancellationToken);
        var content = JsonSerializer.Serialize(document, SettingsDocumentSchema.JsonOptions);
        var export = new SettingsExport
        {
            SettingsFileName = SettingsExportFileNames.Settings(exportedAt),
            SettingsContent = content,
        };
        if (request.Secrets is null)
        {
            return SettingsTransferResult<SettingsExport>.Success(export);
        }

        var archive = await BuildArchiveAsync(document, request.Secrets, cancellationToken);
        if (!archive.IsSuccess)
        {
            return SettingsTransferResult<SettingsExport>.Failure(archive.Error);
        }

        var withSecrets = export with
        {
            SecretsFileName = SettingsExportFileNames.Secrets(exportedAt),
            SecretsArchive = archive.Value.Archive,
            SecretCount = archive.Value.SecretCount,
        };
        return SettingsTransferResult<SettingsExport>.Success(withSecrets);
    }

    private static SettingsTransferError? Validate(SettingsExportRequest request)
    {
        if (!request.Sections.Any)
        {
            return SettingsTransferError.Invalid("Choose at least one part of the settings to export.");
        }

        var secrets = request.Secrets;
        if (secrets is null)
        {
            return null;
        }

        if (!request.Sections.Connections)
        {
            return SettingsTransferError.Invalid("Secrets can be exported only together with server connections.");
        }

        if (!secrets.Confirmed)
        {
            return new SettingsTransferError(SettingsTransferErrorCode.ConfirmationRequired, "Confirm that anyone with the archive and its password can sign in to your servers.");
        }

        var passwordLength = secrets.Password.Length;
        if (passwordLength < SettingsTransferLimits.MinArchivePasswordLength || passwordLength > SettingsTransferLimits.MaxArchivePasswordLength)
        {
            return new SettingsTransferError(SettingsTransferErrorCode.WeakPassword, $"The archive password must be at least {SettingsTransferLimits.MinArchivePasswordLength} characters long.");
        }

        return null;
    }

    private async Task<SettingsTransferResult<SecretsArchiveBuild>> BuildArchiveAsync(SettingsDocument document, SecretsExportOptions secrets, CancellationToken cancellationToken)
    {
        var connections = document.Connections ?? [];
        var connectionIds = new List<Guid>(connections.Count);
        for (var i = 0; i < connections.Count; i++)
        {
            connectionIds.Add(connections[i].Id);
        }

        using var password = Secret.FromString(secrets.Password);
        var archiveRequest = new SecretsArchiveRequest(document.ExportId, connectionIds, password);
        return await archiveBuilder.BuildAsync(archiveRequest, cancellationToken);
    }
}
