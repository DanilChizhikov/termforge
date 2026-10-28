using System.Globalization;

namespace DTech.Core.Docker;

public sealed class ImagePullStream(ContainerLogStream output, DockerOperationRecorder recorder, Guid operationId) : IDockerOutputStream
{
    private bool _disposed;

    public Guid OperationId => operationId;

    public int? ExitCode => output.ExitCode;

    public ValueTask<string?> ReadAsync(CancellationToken cancellationToken)
    {
        return output.ReadAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        var exitCode = output.ExitCode;
        await output.DisposeAsync();
        if (exitCode == 0)
        {
            await recorder.SucceedAsync(operationId);
            return;
        }

        var failure = DescribeFailure(exitCode);
        await recorder.FailAsync(operationId, failure);
    }

    private static DockerError DescribeFailure(int? exitCode)
    {
        if (exitCode is null)
        {
            return new DockerError(DockerErrorCode.CommandFailed, "The pull was cancelled before docker finished.");
        }

        var code = exitCode.Value.ToString(CultureInfo.InvariantCulture);
        return new DockerError(DockerErrorCode.CommandFailed, $"docker pull exited with code {code}.");
    }
}
