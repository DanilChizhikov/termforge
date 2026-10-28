using DTech.Core.Diagnostics;

namespace DTech.Core.Docker;

public sealed class DockerImageService(DockerTargetResolver targetResolver, IRemoteCommandRunnerFactory runnerFactory)
{
    private const int MaxOutputBytes = 1024 * 1024;

    private static readonly TimeSpan ListTimeout = TimeSpan.FromSeconds(20);

    public async Task<DockerResult<DockerImageList>> ListAsync(Guid connectionId, CancellationToken cancellationToken)
    {
        var resolved = await targetResolver.ResolveAsync(connectionId, cancellationToken);
        if (!resolved.IsSuccess)
        {
            return DockerResult<DockerImageList>.Failure(resolved.Error);
        }

        var target = resolved.Value;
        var connected = await runnerFactory.ConnectAsync(target.Target, cancellationToken);
        if (!connected.IsSuccess)
        {
            var connectionError = DockerError.FromSsh(connected.Error!);
            return DockerResult<DockerImageList>.Failure(connectionError);
        }

        await using var runner = connected.Value;
        var request = new RemoteCommandRequest(DockerCommands.ListImages, ListTimeout, MaxOutputBytes);
        var executed = await DockerCommandExecution.RunAsync(runner, request, cancellationToken);
        if (!executed.IsSuccess)
        {
            return DockerResult<DockerImageList>.Failure(executed.Error);
        }

        var images = DockerImageListParser.Parse(executed.Value.StandardOutput);
        var list = new DockerImageList(target.ConnectionId, target.ServerName, images);
        return DockerResult<DockerImageList>.Success(list);
    }
}
