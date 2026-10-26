using DTech.Core.Reconnect;

namespace DTech.Core.SavedConnections;

public static class SavedConnectionValidator
{
    public const int MaxNameLength = 100;
    public const int MaxTextLength = 255;
    public const int MaxPort = 65535;

    public static SavedConnectionDraft Normalize(SavedConnectionDraft draft)
    {
        return draft with
        {
            Name = draft.Name.Trim(),
            Host = draft.Host.Trim(),
            UserName = draft.UserName.Trim(),
            GroupName = NormalizeOptional(draft.GroupName),
            PrivateKeyPath = NormalizeOptional(draft.PrivateKeyPath),
            SshAlias = NormalizeOptional(draft.SshAlias),
        };
    }

    public static SavedConnectionError? Validate(SavedConnectionDraft draft)
    {
        if (draft.Name.Length == 0 || draft.Name.Length > MaxNameLength)
        {
            return SavedConnectionError.Invalid($"The name must be 1 to {MaxNameLength} characters long.");
        }

        if (draft.Host.Length == 0 || draft.Host.Length > MaxTextLength || ContainsWhitespace(draft.Host))
        {
            return SavedConnectionError.Invalid("The host must be a host name or address without spaces.");
        }

        if (draft.Port < 1 || draft.Port > MaxPort)
        {
            return SavedConnectionError.Invalid($"The port must be between 1 and {MaxPort}.");
        }

        if (draft.UserName.Length == 0 || draft.UserName.Length > MaxTextLength)
        {
            return SavedConnectionError.Invalid("The user name is required.");
        }

        if (!Enum.IsDefined(draft.AuthMethod))
        {
            return SavedConnectionError.Invalid("The authentication method is not supported.");
        }

        var reconnectError = ReconnectPolicyValidator.Validate(draft.ReconnectPolicy);
        if (reconnectError is not null)
        {
            return SavedConnectionError.Invalid(reconnectError);
        }

        return ValidateOptionalFields(draft);
    }

    private static SavedConnectionError? ValidateOptionalFields(SavedConnectionDraft draft)
    {
        if (draft.AuthMethod == ConnectionAuthMethod.KeyFile && draft.PrivateKeyPath is null)
        {
            return SavedConnectionError.Invalid("A private key file path is required for key file authentication.");
        }

        if (draft.AuthMethod != ConnectionAuthMethod.KeyFile && draft.PrivateKeyPath is not null)
        {
            return SavedConnectionError.Invalid("A private key file path is allowed only for key file authentication.");
        }

        var groupTooLong = draft.GroupName is not null && draft.GroupName.Length > MaxNameLength;
        var textTooLong = IsTooLong(draft.PrivateKeyPath) || IsTooLong(draft.SshAlias);
        if (groupTooLong || textTooLong)
        {
            return SavedConnectionError.Invalid("A text field is too long.");
        }

        return null;
    }

    private static bool IsTooLong(string? value)
    {
        return value is not null && value.Length > MaxTextLength;
    }

    private static bool ContainsWhitespace(string value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            if (char.IsWhiteSpace(value[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static string? NormalizeOptional(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
