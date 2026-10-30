using System.Globalization;

namespace DTech.Core.SettingsTransfer.Export;

public static class SettingsExportFileNames
{
    public const string PackageExtension = ".tfsettings";

    public static string Package(DateTime exportedAt)
    {
        var date = exportedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return $"termforge-settings-{date}{PackageExtension}";
    }
}
