using DTech.Core.Diagnostics;
using DTech.Core.Safety;

namespace DTech.Core.Docker;

public sealed class ImagePullService
{
    private readonly DockerTargetResolver _targetResolver;
    private readonly IRemoteCommandStreamFactory _streamFactory;
    private readonly DockerOperationRecorder _recorder;
    private readonly ServerWriteGate _writeGate;

    public ImagePullService(DockerTargetResolver targetResolver, IRemoteCommandStreamFactory streamFactory, DockerOperationRecorder recorder, ServerWriteGate writeGate)
    {
        _targetResolver = targetResolver;
        _streamFactory = streamFactory;
        _recorder = recorder;
        _writeGate = writeGate;
    }

    public async Task<DockerResult<ImagePullStream>> OpenAsync(ImagePullRequest request, CancellationToken cancellationToken)
    {
        if (!ImageReference.TryParse(request.Image, out var image))
        {
            var invalid = DockerError.InvalidImage();
            return DockerResult<ImagePullStream>.Failure(invalid);
        }

        var writeIntent = new WriteIntent(request.ConnectionId) { Target = image.Value };
        var denial = await _writeGate.CheckAsync(writeIntent, cancellationToken);
        if (denial is not null)
        {
            var denied = DockerWriteDenials.ToError(denial.Value, $"Confirm that image {image.Value} should be pulled.");
            return DockerResult<ImagePullStream>.Failure(denied);
        }

        var resolved = await _targetResolver.ResolveAsync(request.ConnectionId, cancellationToken);
        if (!resolved.IsSuccess)
        {
            return DockerResult<ImagePullStream>.Failure(resolved.Error);
        }

        var details = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["image"] = image.Value,
        };
        var entry = new DockerOperationEntry(DockerOperationRecorder.PullKind, resolved.Value.ServerName, details);
        var operationId = await _recorder.StartAsync(entry, cancellationToken);
        var command = DockerCommands.Pull(image);
        var started = await _streamFactory.StartAsync(resolved.Value.Target, command, cancellationToken);
        if (!started.IsSuccess)
        {
            var connectionError = DockerError.FromSsh(started.Error!);
            await _recorder.FailAsync(operationId, connectionError);
            return DockerResult<ImagePullStream>.Failure(connectionError);
        }

        var output = new ContainerLogStream(started.Value);
        var stream = new ImagePullStream(output, _recorder, operationId);
        return DockerResult<ImagePullStream>.Success(stream);
    }
}
