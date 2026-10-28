using DTech.Core.Diagnostics;
using DTech.Core.Safety;

namespace DTech.Core.Docker;

public sealed class ImageRemoveService
{
    private const int MaxOutputBytes = 64 * 1024;

    private static readonly TimeSpan RemoveTimeout = TimeSpan.FromMinutes(2);

    private readonly DockerTargetResolver _targetResolver;
    private readonly IRemoteCommandRunnerFactory _runnerFactory;
    private readonly DockerOperationRecorder _recorder;
    private readonly ServerWriteGate _writeGate;

    public ImageRemoveService(DockerTargetResolver targetResolver, IRemoteCommandRunnerFactory runnerFactory, DockerOperationRecorder recorder, ServerWriteGate writeGate)
    {
        _targetResolver = targetResolver;
        _runnerFactory = runnerFactory;
        _recorder = recorder;
        _writeGate = writeGate;
    }

    public async Task<DockerResult<ImageRemoveResult>> RemoveAsync(ImageRemoveRequest request, CancellationToken cancellationToken)
    {
        if (!ImageReference.TryParse(request.Image, out var image))
        {
            var invalid = DockerError.InvalidImage();
            return DockerResult<ImageRemoveResult>.Failure(invalid);
        }

        var writeIntent = new WriteIntent(request.ConnectionId)
        {
            Operation = DangerousOperation.ImageRemove,
            Target = image.Value,
            ConfirmationToken = request.ConfirmationToken,
        };
        var denial = await _writeGate.CheckAsync(writeIntent, cancellationToken);
        if (denial is not null)
        {
            var denied = DockerWriteDenials.ToError(denial.Value, $"Confirm that image {image.Value} should be removed.");
            return DockerResult<ImageRemoveResult>.Failure(denied);
        }

        var resolved = await _targetResolver.ResolveAsync(request.ConnectionId, cancellationToken);
        if (!resolved.IsSuccess)
        {
            return DockerResult<ImageRemoveResult>.Failure(resolved.Error);
        }

        var details = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["image"] = image.Value,
        };
        var entry = new DockerOperationEntry(DockerOperationRecorder.ImageRemoveKind, resolved.Value.ServerName, details);
        var operationId = await _recorder.StartAsync(entry, cancellationToken);
        var outcome = await ExecuteAsync(resolved.Value, image, cancellationToken);
        if (outcome is not null)
        {
            await _recorder.FailAsync(operationId, outcome);
            return DockerResult<ImageRemoveResult>.Failure(outcome);
        }

        await _recorder.SucceedAsync(operationId);
        var completed = new ImageRemoveResult(operationId, image.Value);
        return DockerResult<ImageRemoveResult>.Success(completed);
    }

    private async Task<DockerError?> ExecuteAsync(DockerTarget target, ImageReference image, CancellationToken cancellationToken)
    {
        var connected = await _runnerFactory.ConnectAsync(target.Target, cancellationToken);
        if (!connected.IsSuccess)
        {
            return DockerError.FromSsh(connected.Error!);
        }

        await using var runner = connected.Value;
        var command = DockerCommands.RemoveImage(image);
        var request = new RemoteCommandRequest(command, RemoveTimeout, MaxOutputBytes);
        var executed = await DockerCommandExecution.RunAsync(runner, request, cancellationToken);
        return executed.IsSuccess ? null : executed.Error;
    }
}
