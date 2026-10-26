using DTech.Core.Diagnostics;

namespace DTech.Core.Docker;

public sealed class DockerContainerLister
{
    private const int MaxOutputBytes = 1024 * 1024;

    private static readonly TimeSpan ListTimeout = TimeSpan.FromSeconds(20);

    public async Task<DockerResult<IReadOnlyList<DockerContainer>>> ListAsync(IRemoteCommandRunner runner, CancellationToken cancellationToken)
    {
        var request = new RemoteCommandRequest(DockerCommands.ListContainers, ListTimeout, MaxOutputBytes);
        var executed = await runner.RunAsync(request, cancellationToken);
        if (!executed.IsSuccess)
        {
            var connectionError = DockerError.FromSsh(executed.Error!);
            return DockerResult<IReadOnlyList<DockerContainer>>.Failure(connectionError);
        }

        var result = executed.Value;
        if (!result.Succeeded)
        {
            var failure = DockerFailureClassifier.Classify(result);
            return DockerResult<IReadOnlyList<DockerContainer>>.Failure(failure);
        }

        var containers = DockerContainerListParser.Parse(result.StandardOutput);
        return DockerResult<IReadOnlyList<DockerContainer>>.Success(containers);
    }
}
