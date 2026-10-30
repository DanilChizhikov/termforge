namespace DTech.Core.SettingsTransfer.Document;

public sealed record ExportedConnection
{
    public Guid Id { get; init; }

    public string? Name { get; init; }

    public string? GroupName { get; init; }

    public string? Host { get; init; }

    public int Port { get; init; }

    public string? UserName { get; init; }

    public string? AuthMethod { get; init; }

    public string? PrivateKeyPath { get; init; }

    public Guid? JumpHostId { get; init; }

    public string? SshAlias { get; init; }

    public bool ReadOnly { get; init; }

    public bool AiAccess { get; init; }

    public ExportedReconnectPolicy? Reconnect { get; init; }
}
