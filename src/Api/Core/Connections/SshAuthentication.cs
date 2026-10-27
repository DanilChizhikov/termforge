namespace DTech.Core.Connections;

public abstract record SshAuthentication;

public sealed record PrivateKeyAuthentication(string PrivateKeyPath, string? Passphrase) : SshAuthentication
{
    public override string ToString()
    {
        return $"{nameof(PrivateKeyAuthentication)} {{ {nameof(PrivateKeyPath)} = {PrivateKeyPath} }}";
    }
}

public sealed record AgentAuthentication(string? AgentAddress) : SshAuthentication;

public sealed record PasswordAuthentication(string Password) : SshAuthentication
{
    public override string ToString()
    {
        return nameof(PasswordAuthentication);
    }
}
