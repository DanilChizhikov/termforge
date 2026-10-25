namespace DTech.Core.SshConfig;

public sealed record SshConfigHostEntry(string Alias, string HostName, int Port, string UserName)
{
    public string? IdentityFile { get; init; }

    public string? ProxyJump { get; init; }
}
