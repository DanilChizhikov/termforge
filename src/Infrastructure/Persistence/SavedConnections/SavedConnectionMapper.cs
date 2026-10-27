using DTech.Core.Reconnect;
using DTech.Core.SavedConnections;

namespace DTech.Persistence.SavedConnections;

internal static class SavedConnectionMapper
{
    public static SavedConnection ToModel(SavedConnectionEntity entity)
    {
        var details = new SavedConnectionDraft
        {
            Name = entity.Name,
            Host = entity.Host,
            Port = entity.Port,
            UserName = entity.UserName,
            AuthMethod = entity.AuthMethod,
            GroupName = entity.GroupName,
            PrivateKeyPath = entity.PrivateKeyPath,
            JumpHostId = entity.JumpHostId,
            SshAlias = entity.SshAlias,
            ReconnectPolicy = new ReconnectPolicy(entity.ReconnectMode, entity.ReconnectMaxAttempts, entity.ReconnectInitialDelaySeconds, entity.ReconnectMaxDelaySeconds),
            ReadOnly = entity.ReadOnly,
        };
        return new SavedConnection
        {
            Id = entity.Id,
            Details = details,
            HasPassword = entity.HasPassword,
            LastConnectedAt = entity.LastConnectedAt,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
        };
    }

    public static SavedConnectionEntity ToEntity(SavedConnection model)
    {
        var entity = new SavedConnectionEntity
        {
            Id = model.Id,
            Name = model.Details.Name,
            Host = model.Details.Host,
            UserName = model.Details.UserName,
        };
        CopyTo(model, entity);
        return entity;
    }

    public static void CopyTo(SavedConnection model, SavedConnectionEntity entity)
    {
        var details = model.Details;
        entity.Name = details.Name;
        entity.GroupName = details.GroupName;
        entity.Host = details.Host;
        entity.Port = details.Port;
        entity.UserName = details.UserName;
        entity.AuthMethod = details.AuthMethod;
        entity.PrivateKeyPath = details.PrivateKeyPath;
        entity.JumpHostId = details.JumpHostId;
        entity.SshAlias = details.SshAlias;
        entity.ReconnectMode = details.ReconnectPolicy.Mode;
        entity.ReconnectMaxAttempts = details.ReconnectPolicy.MaxAttempts;
        entity.ReconnectInitialDelaySeconds = details.ReconnectPolicy.InitialDelaySeconds;
        entity.ReconnectMaxDelaySeconds = details.ReconnectPolicy.MaxDelaySeconds;
        entity.ReadOnly = details.ReadOnly;
        entity.HasPassword = model.HasPassword;
        entity.LastConnectedAt = model.LastConnectedAt;
        entity.CreatedAt = model.CreatedAt;
        entity.UpdatedAt = model.UpdatedAt;
    }
}
