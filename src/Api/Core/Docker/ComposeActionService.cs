using DTech.Core.Diagnostics;
using DTech.Core.Safety;

namespace DTech.Core.Docker;

public sealed class ComposeActionService
{
    private const int MaxOutputBytes = 256 * 1024;

    private static readonly TimeSpan RestartTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan RecreateTimeout = TimeSpan.FromMinutes(10);

    private readonly DockerTargetResolver _targetResolver;
    private readonly IRemoteCommandRunnerFactory _runnerFactory;
    private readonly DockerOperationRecorder _recorder;
    private readonly ServerWriteGate _writeGate;
    private readonly ComposeProjectResolver _projectResolver;

    public ComposeActionService(
        DockerTargetResolver targetResolver,
        IRemoteCommandRunnerFactory runnerFactory,
        DockerOperationRecorder recorder,
        ServerWriteGate writeGate,
        ComposeProjectResolver projectResolver)
    {
        _targetResolver = targetResolver;
        _runnerFactory = runnerFactory;
        _recorder = recorder;
        _writeGate = writeGate;
        _projectResolver = projectResolver;
    }

    public static string KindOf(ComposeAction action)
    {
        return action == ComposeAction.Restart ? DockerOperationRecorder.ComposeRestartKind : DockerOperationRecorder.ComposeRecreateKind;
    }

    public async Task<DockerResult<ComposeActionResult>> RunAsync(ComposeActionRequest request, CancellationToken cancellationToken)
    {
        if (!ComposeProjectName.TryParse(request.Project, out var project))
        {
            var invalid = DockerError.InvalidComposeProject();
            return DockerResult<ComposeActionResult>.Failure(invalid);
        }

        var writeIntent = new WriteIntent(request.ConnectionId)
        {
            Operation = request.DangerousOperation,
            Target = project.Value,
            ConfirmationToken = request.ConfirmationToken,
        };
        var denial = await _writeGate.CheckAsync(writeIntent, cancellationToken);
        if (denial is not null)
        {
            var verb = request.Action == ComposeAction.Restart ? "restarted" : "recreated";
            var denied = DockerWriteDenials.ToError(denial.Value, $"Confirm that compose project {project.Value} should be {verb}.");
            return DockerResult<ComposeActionResult>.Failure(denied);
        }

        var resolved = await _targetResolver.ResolveAsync(request.ConnectionId, cancellationToken);
        if (!resolved.IsSuccess)
        {
            return DockerResult<ComposeActionResult>.Failure(resolved.Error);
        }

        var details = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["project"] = project.Value,
            ["action"] = request.Action.ToString().ToLowerInvariant(),
        };
        var kind = KindOf(request.Action);
        var entry = new DockerOperationEntry(kind, resolved.Value.ServerName, details);
        var operationId = await _recorder.StartAsync(entry, cancellationToken);
        var execution = new ComposeExecution(resolved.Value, project, request.Action);
        var outcome = await ExecuteAsync(execution, cancellationToken);
        if (outcome is not null)
        {
            await _recorder.FailAsync(operationId, outcome);
            return DockerResult<ComposeActionResult>.Failure(outcome);
        }

        await _recorder.SucceedAsync(operationId);
        var completed = new ComposeActionResult(operationId, project.Value, request.Action);
        return DockerResult<ComposeActionResult>.Success(completed);
    }

    private async Task<DockerError?> ExecuteAsync(ComposeExecution execution, CancellationToken cancellationToken)
    {
        var connected = await _runnerFactory.ConnectAsync(execution.Target.Target, cancellationToken);
        if (!connected.IsSuccess)
        {
            return DockerError.FromSsh(connected.Error!);
        }

        await using var runner = connected.Value;
        var labels = await _projectResolver.ResolveAsync(runner, execution.Project, cancellationToken);
        if (!labels.IsSuccess)
        {
            return labels.Error;
        }

        var isRecreate = execution.Action == ComposeAction.Recreate;
        if (isRecreate && labels.Value.WorkingDirectory.Length == 0 && labels.Value.ConfigFiles.Count == 0)
        {
            return new DockerError(DockerErrorCode.ComposeProjectNotFound, $"The compose files of {execution.Project.Value} are unknown, so it cannot be recreated from here.");
        }

        var command = DockerCommands.Compose(execution.Action, execution.Project, labels.Value);
        var timeout = isRecreate ? RecreateTimeout : RestartTimeout;
        var request = new RemoteCommandRequest(command, timeout, MaxOutputBytes);
        var executed = await DockerCommandExecution.RunAsync(runner, request, cancellationToken);
        return executed.IsSuccess ? null : executed.Error;
    }

    private readonly record struct ComposeExecution(DockerTarget Target, ComposeProjectName Project, ComposeAction Action);
}
