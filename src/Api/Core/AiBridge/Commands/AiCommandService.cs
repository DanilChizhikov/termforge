using System.Globalization;
using DTech.Core.AiBridge.Approvals;
using DTech.Core.Diagnostics;
using DTech.Core.Files.Search;

namespace DTech.Core.AiBridge.Commands;

public sealed class AiCommandService
{
    private readonly AiServerAccessPolicy _accessPolicy;
    private readonly AiActionGate _actionGate;
    private readonly IRemoteCommandRunnerOpener _runnerOpener;
    private readonly AiOperationRecorder _recorder;

    public AiCommandService(AiServerAccessPolicy accessPolicy, AiActionGate actionGate, IRemoteCommandRunnerOpener runnerOpener, AiOperationRecorder recorder)
    {
        _accessPolicy = accessPolicy;
        _actionGate = actionGate;
        _runnerOpener = runnerOpener;
        _recorder = recorder;
    }

    public async Task<AiBridgeResult<RemoteCommandResult>> RunAsync(AiCommandRequest request, CancellationToken cancellationToken)
    {
        var invalid = Validate(request.Command);
        if (invalid is not null)
        {
            return AiBridgeResult<RemoteCommandResult>.Failure(invalid);
        }

        var command = request.Command!;
        var access = await _accessPolicy.CheckWritableAsync(request.ConnectionId, cancellationToken);
        if (!access.IsSuccess)
        {
            return AiBridgeResult<RemoteCommandResult>.Failure(access.Error);
        }

        var serverName = access.Value.Details.Name;
        var action = new AiActionDescription(request.ConnectionId, serverName, AiActionKind.Command, command);
        var rejection = await _actionGate.AuthorizeAsync(action, cancellationToken);
        if (rejection is not null)
        {
            return AiBridgeResult<RemoteCommandResult>.Failure(rejection);
        }

        var details = new Dictionary<string, string>(StringComparer.Ordinal) { ["command"] = command };
        var entry = new AiOperationEntry(AiOperationRecorder.CommandKind, serverName, details);
        var operationId = await _recorder.StartAsync(entry, cancellationToken);
        var timeout = AiCommandLimits.TimeoutOf(request.TimeoutSeconds);
        var commandRequest = new RemoteCommandRequest(command, timeout, AiCommandLimits.MaxOutputBytes);
        var executed = await ExecuteAsync(request.ConnectionId, commandRequest, cancellationToken);
        await RecordAsync(operationId, executed);
        return executed;
    }

    private async Task<AiBridgeResult<RemoteCommandResult>> ExecuteAsync(Guid connectionId, RemoteCommandRequest request, CancellationToken cancellationToken)
    {
        var opened = await _runnerOpener.OpenAsync(connectionId, cancellationToken);
        if (!opened.IsSuccess)
        {
            var connectionError = AiBridgeErrorMapper.FromFile(opened.Error);
            return AiBridgeResult<RemoteCommandResult>.Failure(connectionError);
        }

        await using var runner = opened.Value;
        var executed = await runner.RunAsync(request, cancellationToken);
        if (!executed.IsSuccess)
        {
            var failure = AiBridgeError.Failed(executed.Error!.Message);
            return AiBridgeResult<RemoteCommandResult>.Failure(failure);
        }

        return AiBridgeResult<RemoteCommandResult>.Success(executed.Value);
    }

    private Task RecordAsync(Guid operationId, AiBridgeResult<RemoteCommandResult> executed)
    {
        if (!executed.IsSuccess)
        {
            return _recorder.FailAsync(operationId, executed.Error.Message);
        }

        var exitCode = executed.Value.ExitCode;
        if (exitCode != 0)
        {
            return _recorder.FailAsync(operationId, $"The command exited with code {exitCode}.");
        }

        var exitCodeText = exitCode.ToString(CultureInfo.InvariantCulture);
        var details = new Dictionary<string, string>(StringComparer.Ordinal) { ["exitCode"] = exitCodeText };
        return _recorder.SucceedAsync(operationId, details);
    }

    private static AiBridgeError? Validate(string? command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return AiBridgeError.Invalid("Enter a command to run.");
        }

        if (command.Length > AiCommandLimits.MaxCommandLength)
        {
            return AiBridgeError.Invalid($"The command is longer than {AiCommandLimits.MaxCommandLength} characters.");
        }

        return null;
    }
}
