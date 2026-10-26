using System.Buffers;
using DTech.Core.Files;
using Renci.SshNet;

namespace DTech.Ssh.Files;

internal sealed class SftpStreamCopier(SftpClient client)
{
    private const int BufferSize = 64 * 1024;

    public async Task WriteAsync(RemoteWriteRequest request, CancellationToken cancellationToken)
    {
        var mode = request.Offset == 0 ? FileMode.Create : FileMode.Open;
        await using var remote = await client.OpenAsync(request.RemotePath, mode, FileAccess.Write, cancellationToken);
        if (request.Offset > 0)
        {
            remote.Seek(request.Offset, SeekOrigin.Begin);
        }

        var copy = new StreamCopy(request.Source, remote, request.Progress);
        await CopyAsync(copy, cancellationToken);
        await remote.FlushAsync(cancellationToken);
    }

    public async Task ReadAsync(RemoteReadRequest request, CancellationToken cancellationToken)
    {
        await using var remote = await client.OpenAsync(request.RemotePath, FileMode.Open, FileAccess.Read, cancellationToken);
        if (request.Offset > 0)
        {
            remote.Seek(request.Offset, SeekOrigin.Begin);
        }

        var copy = new StreamCopy(remote, request.Destination, request.Progress);
        await CopyAsync(copy, cancellationToken);
        await request.Destination.FlushAsync(cancellationToken);
    }

    private static async Task CopyAsync(StreamCopy copy, CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        try
        {
            var readWindow = buffer.AsMemory(0, BufferSize);
            long copied = 0;
            while (true)
            {
                var read = await copy.Source.ReadAsync(readWindow, cancellationToken);
                if (read == 0)
                {
                    return;
                }

                var chunk = buffer.AsMemory(0, read);
                await copy.Destination.WriteAsync(chunk, cancellationToken);
                copied += read;
                copy.Progress?.Report(copied);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private readonly record struct StreamCopy(Stream Source, Stream Destination, IProgress<long>? Progress);
}
