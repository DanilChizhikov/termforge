using DTech.Core.Credentials;

namespace DTech.Entrypoint.SavedConnections;

public sealed class SavedConnectionRequest
{
    public string? Name { get; set; }

    public string? GroupName { get; set; }

    public string? Host { get; set; }

    public int? Port { get; set; }

    public string? UserName { get; set; }

    public string? AuthMethod { get; set; }

    public string? PrivateKeyPath { get; set; }

    public Guid? JumpHostId { get; set; }

    public string? SshAlias { get; set; }

    public string? Password { get; set; }

    public override string ToString()
    {
        return $"{nameof(SavedConnectionRequest)} {{ {nameof(Name)} = {Name}, {nameof(Password)} = {Secret.Redacted} }}";
    }
}
