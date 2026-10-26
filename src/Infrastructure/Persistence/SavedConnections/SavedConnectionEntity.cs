using DTech.Core.SavedConnections;

namespace DTech.Persistence.SavedConnections;

public sealed class SavedConnectionEntity
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    public string? GroupName { get; set; }

    public required string Host { get; set; }

    public int Port { get; set; }

    public required string UserName { get; set; }

    public ConnectionAuthMethod AuthMethod { get; set; }

    public string? PrivateKeyPath { get; set; }

    public Guid? JumpHostId { get; set; }

    public string? SshAlias { get; set; }

    public bool HasPassword { get; set; }

    public DateTime? LastConnectedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
