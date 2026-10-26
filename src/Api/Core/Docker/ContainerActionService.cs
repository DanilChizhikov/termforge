using DTech.Core.Diagnostics;

namespace DTech.Core.Docker;

public sealed class ContainerActionService(DockerTargetResolver targetResolver, IRemoteCommandRunnerFactory runnerFactory, DockerOperationRecorder recorder)
{
    private const int MaxOutputBytes = 64 * 1024;

    private static readonly TimeSpan ActionTimeout = TimeSpan.FromSeconds(90);

    public async Task<DockerResult<ContainerActionResult>> RunAsync(ContainerActionRequest request, CancellationToken cancellationToken)
    {
        if (!ContainerReference.TryParse(request.Container, out var container))
        {
            var invalid = DockerError.InvalidContainer();
            return DockerResult<ContainerActionResult>.Failure(invalid);
        }

        if (request.RequiresConfirmation && !request.Confirmed)
        {
            var outcomeDescription = DescribePastTense(request.Action);
            var unconfirmed = new DockerError(DockerErrorCode.ConfirmationRequired, $"Confirm that {container.Value} should be {outcomeDescription}.");
            return DockerResult<ContainerActionResult>.Failure(unconfirmed);
        }

        var resolved = await targetResolver.ResolveAsync(request.ConnectionId, cancellationToken);
        if (!resolved.IsSuccess)
        {
            return DockerResult<ContainerActionResult>.Failure(resolved.Error);
        }

        var start = new DockerOperationStart(request.Action, resolved.Value.ServerName, container);
        var operationId = await recorder.StartAsync(start, cancellationToken);
        var outcome = await ExecuteAsync(resolved.Value, start, cancellationToken);
        if (outcome is not null)
        {
            await recorder.FailAsync(operationId, outcome);
            return DockerResult<ContainerActionResult>.Failure(outcome);
        }

        await recorder.SucceedAsync(operationId);
        var completed = new ContainerActionResult(operationId, container.Value, request.Action);
        return DockerResult<ContainerActionResult>.Success(completed);
    }

    private async Task<DockerError?> ExecuteAsync(DockerTarget target, DockerOperationStart start, CancellationToken cancellationToken)
    {
        var connected = await runnerFactory.ConnectAsync(target.Target, cancellationToken);
        if (!connected.IsSuccess)
        {
            return DockerError.FromSsh(connected.Error!);
        }

        await using var runner = connected.Value;
        var command = DockerCommands.Action(start.Action, start.Container);
        var request = new RemoteCommandRequest(command, ActionTimeout, MaxOutputBytes);
        var executed = await runner.RunAsync(request, cancellationToken);
        if (!executed.IsSuccess)
        {
            return DockerError.FromSsh(executed.Error!);
        }

        return executed.Value.Succeeded ? null : DockerFailureClassifier.Classify(executed.Value);
    }

    private static string DescribePastTense(DockerContainerAction action)
    {
        return action == DockerContainerAction.Stop ? "stopped" : "restarted";
    }
}
