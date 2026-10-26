using DTech.Core.SavedConnections;

namespace DTech.Entrypoint.SavedConnections;

public sealed class SavedConnectionResponse
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public string? GroupName { get; init; }

    public required string Host { get; init; }

    public int Port { get; init; }

    public required string UserName { get; init; }

    public required string AuthMethod { get; init; }

    public string? PrivateKeyPath { get; init; }

    public Guid? JumpHostId { get; init; }

    public string? SshAlias { get; init; }

    public bool HasPassword { get; init; }

    public required string ReconnectMode { get; init; }

    public int ReconnectMaxAttempts { get; init; }

    public int ReconnectInitialDelaySeconds { get; init; }

    public int ReconnectMaxDelaySeconds { get; init; }

    public DateTime? LastConnectedAt { get; init; }

    public static SavedConnectionResponse From(SavedConnection connection)
    {
        var details = connection.Details;
        return new SavedConnectionResponse
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
            HasPassword = connection.HasPassword,
            ReconnectMode = details.ReconnectPolicy.Mode.ToString(),
            ReconnectMaxAttempts = details.ReconnectPolicy.MaxAttempts,
            ReconnectInitialDelaySeconds = details.ReconnectPolicy.InitialDelaySeconds,
            ReconnectMaxDelaySeconds = details.ReconnectPolicy.MaxDelaySeconds,
            LastConnectedAt = connection.LastConnectedAt,
        };
    }
}
