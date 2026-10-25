using DotNet.Testcontainers.Containers;
using DTech.Core.Connections;

namespace DTech.Ssh.IntegrationTests.Harness;

internal sealed class SshServerContainer : IAsyncDisposable
{
    public const string UserName = "tester";
    public const string Password = "tester-password";
    public const string HomeDirectory = "/home/tester";
    public const int SshPort = 22;

    private const string UserPrivateKeyPath = "/keys/tester_ed25519";
    private const string HostPublicKeyPath = "/etc/ssh/ssh_host_ed25519_key.pub";

    private readonly IContainer _container;

    private SshServerContainer(IContainer container, SshServerIdentity identity)
    {
        _container = container;
        Identity = identity;
        var mappedPort = container.GetMappedPublicPort(SshPort);
        Endpoint = new SshEndpoint(container.Hostname, mappedPort);
    }

    public SshServerIdentity Identity { get; }

    public SshEndpoint Endpoint { get; }

    public SshEndpoint NetworkEndpoint => new(Identity.NetworkAlias, SshPort);

    public static async Task<SshServerContainer> StartAsync(IContainer container, SshServerIdentityRequest request, CancellationToken cancellationToken)
    {
        await container.StartAsync(cancellationToken);
        var privateKey = await container.ReadFileAsync(UserPrivateKeyPath, cancellationToken);
        var privateKeyPath = Path.Combine(request.KeyDirectory, $"{request.NetworkAlias}_ed25519");
        await TestKeyFiles.WritePrivateKeyAsync(privateKeyPath, privateKey, cancellationToken);
        var hostPublicKeyBytes = await container.ReadFileAsync(HostPublicKeyPath, cancellationToken);
        var hostPublicKeyLine = System.Text.Encoding.ASCII.GetString(hostPublicKeyBytes).Trim();
        var hostKey = OpenSshPublicKey.ToHostKey(hostPublicKeyLine);
        var hostName = await ExecuteAsync(container, "hostname", cancellationToken);
        var identity = new SshServerIdentity(request.NetworkAlias, hostName, privateKeyPath)
        {
            HostKey = hostKey,
            HostPublicKeyLine = hostPublicKeyLine,
        };
        return new SshServerContainer(container, identity);
    }

    public SshConnectionTarget CreateTarget(SshAuthentication authentication)
    {
        return new SshConnectionTarget(Endpoint, UserName, authentication);
    }

    public SshConnectionTarget CreateKeyTarget()
    {
        var authentication = new PrivateKeyAuthentication(Identity.PrivateKeyPath, null);
        return CreateTarget(authentication);
    }

    public SshConnectionTarget CreateNetworkKeyTarget()
    {
        var authentication = new PrivateKeyAuthentication(Identity.PrivateKeyPath, null);
        return new SshConnectionTarget(NetworkEndpoint, UserName, authentication);
    }

    public Task<string> ExecuteAsync(string command, CancellationToken cancellationToken)
    {
        return ExecuteAsync(_container, command, cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        return _container.DisposeAsync();
    }

    private static async Task<string> ExecuteAsync(IContainer container, string command, CancellationToken cancellationToken)
    {
        var commandLine = new[] { "/bin/sh", "-c", command };
        var result = await container.ExecAsync(commandLine, cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"Command '{command}' failed with {result.ExitCode}: {result.Stderr}");
        }

        return result.Stdout.Trim();
    }
}
