using DTech.Core.Credentials;
using DTech.Core.SettingsTransfer.Export;

namespace DTech.Entrypoint.SettingsTransfer;

public sealed record SettingsExportResponse(ExportedFileResponse File, string Key, int SecretCount)
{
    public static SettingsExportResponse From(SettingsExport export)
    {
        var encoded = Convert.ToBase64String(export.Package);
        var file = new ExportedFileResponse(export.FileName, encoded);
        return new SettingsExportResponse(file, export.Key, export.SecretCount);
    }

    public override string ToString()
    {
        return $"{nameof(SettingsExportResponse)} {{ {nameof(File)} = {File.FileName}, {nameof(Key)} = {Secret.Redacted}, {nameof(SecretCount)} = {SecretCount} }}";
    }
}
