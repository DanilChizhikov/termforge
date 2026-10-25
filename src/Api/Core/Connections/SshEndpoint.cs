namespace DTech.Core.Connections;

public sealed record SshEndpoint(string Host, int Port)
{
    public const int DefaultPort = 22;

    public override string ToString()
    {
        return $"{Host}:{Port}";
    }
}
