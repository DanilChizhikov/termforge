using DTech.Core.SettingsTransfer.Export;

namespace DTech.Entrypoint.SettingsTransfer;

public sealed record SettingsExportResponse(ExportedFileResponse Settings, ExportedArchiveResponse? Secrets)
{
    public static SettingsExportResponse From(SettingsExport export)
    {
        var settings = new ExportedFileResponse(export.SettingsFileName, export.SettingsContent);
        if (export.SecretsArchive is null || export.SecretsFileName is null)
        {
            return new SettingsExportResponse(settings, null);
        }

        var encoded = Convert.ToBase64String(export.SecretsArchive);
        var secrets = new ExportedArchiveResponse(export.SecretsFileName, encoded, export.SecretCount);
        return new SettingsExportResponse(settings, secrets);
    }
}
