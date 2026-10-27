using DTech.Core.SavedConnections;
using DTech.Core.SettingsTransfer.Document;

namespace DTech.Core.SettingsTransfer.Import;

public static class ImportedConnectionValidator
{
    public static string? Validate(ExportedConnection? connection)
    {
        if (connection is null || connection.Id == Guid.Empty)
        {
            return "A server connection has no valid id.";
        }

        var draft = ExportedConnectionMapper.TryToDraft(connection);
        if (draft is null)
        {
            return "A server connection is missing its name, host, user, authentication or reconnect settings.";
        }

        var invalid = SavedConnectionValidator.Validate(draft);
        if (invalid is not null)
        {
            return $"Server '{draft.Name}': {invalid.Message}";
        }

        return ValidateUntrustedText(draft);
    }

    private static string? ValidateUntrustedText(SavedConnectionDraft draft)
    {
        var hasControlCharacters = ImportedTextRules.HasControlCharacters(draft.Name)
            || ImportedTextRules.HasControlCharacters(draft.Host)
            || ImportedTextRules.HasControlCharacters(draft.UserName)
            || ImportedTextRules.HasControlCharacters(draft.GroupName)
            || ImportedTextRules.HasControlCharacters(draft.PrivateKeyPath)
            || ImportedTextRules.HasControlCharacters(draft.SshAlias);
        if (hasControlCharacters)
        {
            return $"Server '{draft.Name}' contains control characters.";
        }

        var looksLikeOption = ImportedTextRules.StartsLikeOption(draft.Host)
            || ImportedTextRules.StartsLikeOption(draft.UserName)
            || ImportedTextRules.StartsLikeOption(draft.SshAlias)
            || ImportedTextRules.StartsLikeOption(draft.PrivateKeyPath);
        if (looksLikeOption)
        {
            return $"Server '{draft.Name}': the host, user, SSH alias and key path must not start with '-'.";
        }

        return null;
    }
}
