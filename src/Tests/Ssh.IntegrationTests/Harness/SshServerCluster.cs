using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;
using DotNet.Testcontainers.Networks;

namespace DTech.Ssh.IntegrationTests.Harness;

internal sealed class SshServerCluster : IAsyncDisposable
{
    public const string TargetAlias = "ssh-target";
    public const string JumpAlias = "ssh-jump";

    private const string ServerDirectoryName = "SshServer";
    private const string ReadyLogMessage = "Server listening on";

    private readonly INetwork _network;
    private readonly IFutureDockerImage _image;
    private readonly string _keyDirectory;

    private SshServerCluster(SshServerClusterParts parts, SshServerContainer target, SshServerContainer jump)
    {
        _network = parts.Network;
        _image = parts.Image;
        _keyDirectory = parts.KeyDirectory;
        Target = target;
        Jump = jump;
    }

    public SshServerContainer Target { get; }

    public SshServerContainer Jump { get; }

    public string KeyDirectory => _keyDirectory;

    public static async Task<SshServerCluster> StartAsync(CancellationToken cancellationToken)
    {
        var serverDirectory = Path.Combine(AppContext.BaseDirectory, ServerDirectoryName);
        var image = new ImageFromDockerfileBuilder()
            .WithDockerfileDirectory(serverDirectory)
            .WithDockerfile("Dockerfile")
            .Build();
        await image.CreateAsync(cancellationToken);

        var network = new NetworkBuilder().Build();
        await network.CreateAsync(cancellationToken);

        var keyDirectory = TestKeyFiles.CreateDirectory();
        var parts = new SshServerClusterParts(network, image, keyDirectory);
        var target = await StartServerAsync(parts, TargetAlias, cancellationToken);
        var jump = await StartServerAsync(parts, JumpAlias, cancellationToken);
        return new SshServerCluster(parts, target, jump);
    }

    public async ValueTask DisposeAsync()
    {
        await Target.DisposeAsync();
        await Jump.DisposeAsync();
        await _network.DisposeAsync();
        await _image.DisposeAsync();
        TestKeyFiles.DeleteDirectory(_keyDirectory);
    }

    private static Task<SshServerContainer> StartServerAsync(SshServerClusterParts parts, string networkAlias, CancellationToken cancellationToken)
    {
        var waitStrategy = Wait.ForUnixContainer().UntilMessageIsLogged(ReadyLogMessage);
        var container = new ContainerBuilder(parts.Image)
            .WithNetwork(parts.Network)
            .WithNetworkAliases(networkAlias)
            .WithHostname(networkAlias)
            .WithPortBinding(SshServerContainer.SshPort, true)
            .WithWaitStrategy(waitStrategy)
            .Build();
        var request = new SshServerIdentityRequest(networkAlias, parts.KeyDirectory);
        return SshServerContainer.StartAsync(container, request, cancellationToken);
    }

    private sealed record SshServerClusterParts(INetwork Network, IFutureDockerImage Image, string KeyDirectory);
}
