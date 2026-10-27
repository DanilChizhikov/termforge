using DTech.Core.Diagnostics;
using DTech.Core.Safety;

namespace DTech.Core.Docker;

public sealed class ContainerActionService
{
    private const int MaxOutputBytes = 64 * 1024;

    private static readonly TimeSpan ActionTimeout = TimeSpan.FromSeconds(90);

    private readonly DockerTargetResolver _targetResolver;
    private readonly IRemoteCommandRunnerFactory _runnerFactory;
    private readonly DockerOperationRecorder _recorder;
    private readonly ServerWriteGate _writeGate;

    public ContainerActionService(DockerTargetResolver targetResolver, IRemoteCommandRunnerFactory runnerFactory, DockerOperationRecorder recorder, ServerWriteGate writeGate)
    {
        _targetResolver = targetResolver;
        _runnerFactory = runnerFactory;
        _recorder = recorder;
        _writeGate = writeGate;
    }

    public async Task<DockerResult<ContainerActionResult>> RunAsync(ContainerActionRequest request, CancellationToken cancellationToken)
    {
        if (!ContainerReference.TryParse(request.Container, out var container))
        {
            var invalid = DockerError.InvalidContainer();
            return DockerResult<ContainerActionResult>.Failure(invalid);
        }

        var writeIntent = new WriteIntent(request.ConnectionId)
        {
            Operation = request.DangerousOperation,
            Target = container.Value,
            ConfirmationToken = request.ConfirmationToken,
        };
        var denial = await _writeGate.CheckAsync(writeIntent, cancellationToken);
        if (denial is not null)
        {
            var denied = ToError(denial.Value, request.Action, container.Value);
            return DockerResult<ContainerActionResult>.Failure(denied);
        }

        var resolved = await _targetResolver.ResolveAsync(request.ConnectionId, cancellationToken);
        if (!resolved.IsSuccess)
        {
            return DockerResult<ContainerActionResult>.Failure(resolved.Error);
        }

        var start = new DockerOperationStart(request.Action, resolved.Value.ServerName, container);
        var operationId = await _recorder.StartAsync(start, cancellationToken);
        var outcome = await ExecuteAsync(resolved.Value, start, cancellationToken);
        if (outcome is not null)
        {
            await _recorder.FailAsync(operationId, outcome);
            return DockerResult<ContainerActionResult>.Failure(outcome);
        }

        await _recorder.SucceedAsync(operationId);
        var completed = new ContainerActionResult(operationId, container.Value, request.Action);
        return DockerResult<ContainerActionResult>.Success(completed);
    }

    private async Task<DockerError?> ExecuteAsync(DockerTarget target, DockerOperationStart start, CancellationToken cancellationToken)
    {
        var connected = await _runnerFactory.ConnectAsync(target.Target, cancellationToken);
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

    private static DockerError ToError(WriteDenial denial, DockerContainerAction action, string container)
    {
        if (denial == WriteDenial.ReadOnlyServer)
        {
            return DockerError.ReadOnlyServer();
        }

        var outcomeDescription = DescribePastTense(action);
        return new DockerError(DockerErrorCode.ConfirmationRequired, $"Confirm that {container} should be {outcomeDescription}.");
    }

    private static string DescribePastTense(DockerContainerAction action)
    {
        return action == DockerContainerAction.Stop ? "stopped" : "restarted";
    }
}
