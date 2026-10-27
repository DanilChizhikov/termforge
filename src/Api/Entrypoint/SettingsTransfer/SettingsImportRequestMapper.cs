using DTech.Core.SettingsTransfer;
using DTech.Core.SettingsTransfer.Import;

namespace DTech.Entrypoint.SettingsTransfer;

internal static class SettingsImportRequestMapper
{
    private const int MaxResolutions = SettingsTransferLimits.MaxConnections;

    public static SettingsTransferResult<SettingsImportRequest> ToRequest(SettingsImportBody body)
    {
        if (string.IsNullOrWhiteSpace(body.SettingsFile))
        {
            return Invalid("Choose a TermForge settings file to import.");
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

        var secrets = ReadSecrets(body);
        if (!secrets.IsSuccess)
        {
            return SettingsTransferResult<SettingsImportRequest>.Failure(secrets.Error);
        }

        var allSections = new SettingsSections(true, true, true, true);
        var sections = body.Sections?.ToSections() ?? allSections;
        var options = new SettingsImportOptions(mode, sections, resolutions) { ConfirmReplace = body.ConfirmReplace };
        var request = new SettingsImportRequest(body.SettingsFile, secrets.Value, options);
        body.Password = null;
        return SettingsTransferResult<SettingsImportRequest>.Success(request);
    }

    private static SettingsTransferResult<SecretsImportInput?> ReadSecrets(SettingsImportBody body)
    {
        if (string.IsNullOrEmpty(body.SecretsArchive))
        {
            return SettingsTransferResult<SecretsImportInput?>.Success(null);
        }

        var maxEncodedLength = ((SettingsTransferLimits.MaxSecretsArchiveBytes + 2) / 3 * 4) + 4;
        if (body.SecretsArchive.Length > maxEncodedLength)
        {
            var tooLarge = SettingsTransferError.TooLarge("secrets archive", SettingsTransferLimits.MaxSecretsArchiveBytes);
            return SettingsTransferResult<SecretsImportInput?>.Failure(tooLarge);
        }

        if (string.IsNullOrEmpty(body.Password))
        {
            var missingPassword = SettingsTransferError.Invalid("Enter the archive password to import the secrets archive.");
            return SettingsTransferResult<SecretsImportInput?>.Failure(missingPassword);
        }

        byte[] archive;
        try
        {
            archive = Convert.FromBase64String(body.SecretsArchive);
        }
        catch (FormatException)
        {
            var damaged = SettingsTransferError.ArchiveDamaged();
            return SettingsTransferResult<SecretsImportInput?>.Failure(damaged);
        }

        var input = new SecretsImportInput(archive, body.Password);
        return SettingsTransferResult<SecretsImportInput?>.Success(input);
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
