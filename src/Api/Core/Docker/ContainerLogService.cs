using DTech.Core.Diagnostics;

namespace DTech.Core.Docker;

public sealed class ContainerLogService(DockerTargetResolver targetResolver, IRemoteCommandStreamFactory streamFactory)
{
    public async Task<DockerResult<ContainerLogStream>> OpenAsync(ContainerLogRequest request, CancellationToken cancellationToken)
    {
        if (!ContainerReference.TryParse(request.Container, out var container))
        {
            var invalid = DockerError.InvalidContainer();
            return DockerResult<ContainerLogStream>.Failure(invalid);
        }

        var resolved = await targetResolver.ResolveAsync(request.ConnectionId, cancellationToken);
        if (!resolved.IsSuccess)
        {
            return DockerResult<ContainerLogStream>.Failure(resolved.Error);
        }

        var command = DockerCommands.Logs(container, request.Options);
        var started = await streamFactory.StartAsync(resolved.Value.Target, command, cancellationToken);
        if (!started.IsSuccess)
        {
            var connectionError = DockerError.FromSsh(started.Error!);
            return DockerResult<ContainerLogStream>.Failure(connectionError);
        }

        var stream = new ContainerLogStream(started.Value);
        return DockerResult<ContainerLogStream>.Success(stream);
    }
}
