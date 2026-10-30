using DTech.Core.Reconnect;

namespace DTech.Core.SavedConnections;

public sealed record SavedConnectionDraft
{
    public required string Name { get; init; }

    public required string Host { get; init; }

    public int Port { get; init; } = 22;

    public required string UserName { get; init; }

    public ConnectionAuthMethod AuthMethod { get; init; } = ConnectionAuthMethod.Agent;

    public string? GroupName { get; init; }

    public string? PrivateKeyPath { get; init; }

    public Guid? JumpHostId { get; init; }

    public string? SshAlias { get; init; }

    public ReconnectPolicy ReconnectPolicy { get; init; } = ReconnectPolicy.Default;

    public bool ReadOnly { get; init; }

    public bool AiAccess { get; init; }
}
