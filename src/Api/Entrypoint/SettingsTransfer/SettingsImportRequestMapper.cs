using DTech.Core.SettingsTransfer;
using DTech.Core.SettingsTransfer.Import;

namespace DTech.Entrypoint.SettingsTransfer;

internal static class SettingsImportRequestMapper
{
    private const int MaxResolutions = SettingsTransferLimits.MaxConnections;

    public static SettingsTransferResult<SettingsImportRequest> ToRequest(SettingsImportBody body)
    {
        var package = ReadPackage(body.File);
        if (!package.IsSuccess)
        {
            return SettingsTransferResult<SettingsImportRequest>.Failure(package.Error);
        }

        if (string.IsNullOrWhiteSpace(body.Key))
        {
            return Invalid("Enter the key shown when the settings were exported.");
        }

        var mode = ImportMode.Merge;
        if (body.Mode is not null && !TryParseName(body.Mode, out mode))
        {
            return Invalid($"The import mode '{body.Mode}' is not supported. Use merge or replace.");
        }

        var resolutions = ParseResolutions(body.Resolutions);
        if (resolutions is null)
        {
            return Invalid("A conflict resolution is invalid. Use keepExisting, useImported or keepBoth.");
        }

        var allSections = new SettingsSections(true, true, true, true);
        var sections = body.Sections?.ToSections() ?? allSections;
        var options = new SettingsImportOptions(mode, sections, resolutions) { ConfirmReplace = body.ConfirmReplace };
        var request = new SettingsImportRequest(package.Value, body.Key, options);
        body.Key = null;
        return SettingsTransferResult<SettingsImportRequest>.Success(request);
    }

    private static SettingsTransferResult<byte[]> ReadPackage(string? encoded)
    {
        if (string.IsNullOrWhiteSpace(encoded))
        {
            var missing = SettingsTransferError.Invalid("Choose a TermForge settings file to import.");
            return SettingsTransferResult<byte[]>.Failure(missing);
        }

        var maxEncodedLength = ((SettingsTransferLimits.MaxPackageBytes + 2) / 3 * 4) + 4;
        if (encoded.Length > maxEncodedLength)
        {
            var tooLarge = SettingsTransferError.TooLarge("settings file", SettingsTransferLimits.MaxPackageBytes);
            return SettingsTransferResult<byte[]>.Failure(tooLarge);
        }

        try
        {
            var package = Convert.FromBase64String(encoded);
            return SettingsTransferResult<byte[]>.Success(package);
        }
        catch (FormatException)
        {
            var damaged = SettingsTransferError.FileDamaged();
            return SettingsTransferResult<byte[]>.Failure(damaged);
        }
    }

    private static bool TryParseName<TEnum>(string value, out TEnum parsed)
        where TEnum : struct, Enum
    {
        parsed = default;
        if (value.Length == 0 || char.IsDigit(value[0]) || value[0] == '-')
        {
            return false;
        }

        return Enum.TryParse(value, true, out parsed) && Enum.IsDefined(parsed);
    }

    private static Dictionary<Guid, ConnectionConflictResolution>? ParseResolutions(Dictionary<string, string>? values)
    {
        var resolutions = new Dictionary<Guid, ConnectionConflictResolution>();
        if (values is null)
        {
            return resolutions;
        }

        if (values.Count > MaxResolutions)
        {
            return null;
        }

        foreach (var pair in values)
        {
            var validId = Guid.TryParse(pair.Key, out var id);
            var validValue = TryParseName(pair.Value ?? string.Empty, out ConnectionConflictResolution resolution);
            if (!validId || !validValue)
            {
                return null;
            }

            resolutions[id] = resolution;
        }

        return resolutions;
    }

    private static SettingsTransferResult<SettingsImportRequest> Invalid(string message)
    {
        var error = SettingsTransferError.Invalid(message);
        return SettingsTransferResult<SettingsImportRequest>.Failure(error);
    }
}
