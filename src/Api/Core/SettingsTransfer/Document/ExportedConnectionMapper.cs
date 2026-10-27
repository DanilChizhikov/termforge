using DTech.Core.Reconnect;
using DTech.Core.SavedConnections;

namespace DTech.Core.SettingsTransfer.Document;

public static class ExportedConnectionMapper
{
    public static ExportedConnection ToExported(SavedConnection connection)
    {
        var details = connection.Details;
        var policy = details.ReconnectPolicy;
        var reconnect = new ExportedReconnectPolicy
        {
            Mode = policy.Mode.ToString(),
            MaxAttempts = policy.MaxAttempts,
            InitialDelaySeconds = policy.InitialDelaySeconds,
            MaxDelaySeconds = policy.MaxDelaySeconds,
        };
        return new ExportedConnection
        {
            Id = connection.Id,
            Name = details.Name,
            GroupName = details.GroupName,
            Host = details.Host,
            Port = details.Port,
            UserName = details.UserName,
            AuthMethod = details.AuthMethod.ToString(),
            PrivateKeyPath = details.PrivateKeyPath,
            JumpHostId = details.JumpHostId,
            SshAlias = details.SshAlias,
            Reconnect = reconnect,
        };
    }

    public static SavedConnectionDraft? TryToDraft(ExportedConnection connection)
    {
        var reconnect = connection.Reconnect;
        if (connection.Name is null || connection.Host is null || connection.UserName is null || reconnect is null)
        {
            return null;
        }

        if (!TryParseEnum(connection.AuthMethod, out ConnectionAuthMethod authMethod) || !TryParseEnum(reconnect.Mode, out ReconnectMode mode))
        {
            return null;
        }

        var policy = new ReconnectPolicy(mode, reconnect.MaxAttempts, reconnect.InitialDelaySeconds, reconnect.MaxDelaySeconds);
        var draft = new SavedConnectionDraft
        {
            Name = connection.Name,
            Host = connection.Host,
            Port = connection.Port,
            UserName = connection.UserName,
            AuthMethod = authMethod,
            GroupName = connection.GroupName,
            PrivateKeyPath = connection.PrivateKeyPath,
            JumpHostId = connection.JumpHostId,
            SshAlias = connection.SshAlias,
            ReconnectPolicy = policy,
        };
        return SavedConnectionValidator.Normalize(draft);
    }

    private static bool TryParseEnum<TEnum>(string? value, out TEnum parsed)
        where TEnum : struct, Enum
    {
        parsed = default;
        if (value is null || value.Length == 0 || char.IsDigit(value[0]) || value[0] == '-')
        {
            return false;
        }

        return Enum.TryParse(value, true, out parsed) && Enum.IsDefined(parsed);
    }
}
