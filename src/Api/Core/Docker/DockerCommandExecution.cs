using DTech.Core.Diagnostics;

namespace DTech.Core.Docker;

public static class DockerCommandExecution
{
    public static async Task<DockerResult<RemoteCommandResult>> RunAsync(IRemoteCommandRunner runner, RemoteCommandRequest request, CancellationToken cancellationToken)
    {
        var executed = await runner.RunAsync(request, cancellationToken);
        if (!executed.IsSuccess)
        {
            var connectionError = DockerError.FromSsh(executed.Error!);
            return DockerResult<RemoteCommandResult>.Failure(connectionError);
        }

        if (!executed.Value.Succeeded)
        {
            var failure = DockerFailureClassifier.Classify(executed.Value);
            return DockerResult<RemoteCommandResult>.Failure(failure);
        }

        return DockerResult<RemoteCommandResult>.Success(executed.Value);
    }
}
