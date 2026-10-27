using System.Globalization;

namespace DTech.Core.SettingsTransfer.Export;

public static class SettingsExportFileNames
{
    public const string SecretsArchiveExtension = ".tfvault";

    public static string Settings(DateTime exportedAt)
    {
        var date = exportedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return $"termforge-settings-{date}.json";
    }

    public static string Secrets(DateTime exportedAt)
    {
        var date = exportedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return $"termforge-secrets-{date}{SecretsArchiveExtension}";
    }
}
