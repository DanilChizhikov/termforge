using DTech.Core.Diagnostics;

namespace DTech.Core.Docker;

public sealed class ComposeProjectResolver
{
    private const int MaxOutputBytes = 1024 * 1024;

    private static readonly TimeSpan ListTimeout = TimeSpan.FromSeconds(20);

    public async Task<DockerResult<ComposeLabels>> ResolveAsync(IRemoteCommandRunner runner, ComposeProjectName project, CancellationToken cancellationToken)
    {
        var command = DockerCommands.ComposeProjectContainers(project);
        var request = new RemoteCommandRequest(command, ListTimeout, MaxOutputBytes);
        var executed = await DockerCommandExecution.RunAsync(runner, request, cancellationToken);
        if (!executed.IsSuccess)
        {
            return DockerResult<ComposeLabels>.Failure(executed.Error);
        }

        var containers = DockerContainerListParser.Parse(executed.Value.StandardOutput);
        for (var i = 0; i < containers.Count; i++)
        {
            var labels = containers[i].Compose;
            if (labels is not null && labels.Project == project.Value)
            {
                return DockerResult<ComposeLabels>.Success(labels);
            }
        }

        var notFound = new DockerError(DockerErrorCode.ComposeProjectNotFound, $"The compose project {project.Value} has no containers on the server. Refresh the container list.");
        return DockerResult<ComposeLabels>.Failure(notFound);
    }
}
