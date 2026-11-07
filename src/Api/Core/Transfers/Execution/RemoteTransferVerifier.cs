using System.Security.Cryptography;
using DTech.Core.Files;

namespace DTech.Core.Transfers.Execution;

public sealed class RemoteTransferVerifier
{
    public async Task<FileResult> CheckSourceAsync(TransferContext context, TransferItem item, CancellationToken cancellationToken)
    {
        var stat = await context.Client.StatAsync(item.SourcePath, cancellationToken);
        if (!stat.IsSuccess)
        {
            var error = FileErrorMapper.FromSsh(stat.Error!, item.SourcePath);
            return FileResult.Failure(error);
        }

        return stat.Value.Size == item.Size && stat.Value.LastWriteTimeUtc == item.ModifiedUtc
            ? FileResult.Success()
            : RestartRequired();
    }

    public async Task<FileResult> VerifyAsync(TransferContext context, TransferCopy copy, CancellationToken cancellationToken)
    {
        var before = await CheckSourceAsync(context, copy.Item, cancellationToken);
        if (!before.IsSuccess) return before;
        using var source = new PrefixHashStream(copy.Offset, copy.Progress, 0);
        var read = new RemoteReadRequest(copy.Item.SourcePath, source, 0);
        var sourceResult = await context.Client.ReadFileAsync(read, cancellationToken);
        if (!sourceResult.IsSuccess)
        {
            var error = FileErrorMapper.FromSsh(sourceResult.Error!, copy.Item.SourcePath);
            return FileResult.Failure(error);
        }

        using var target = new PrefixHashStream(copy.Offset, copy.Progress, copy.Item.Size);
        var targetRead = new RemoteReadRequest(copy.PartialPath, target, 0);
        var targetResult = await context.TargetClient!.ReadFileAsync(targetRead, cancellationToken);
        if (!targetResult.IsSuccess)
        {
            var error = FileErrorMapper.FromSsh(targetResult.Error!, copy.PartialPath);
            return FileResult.Failure(error);
        }

        var sourceHash = source.Hash();
        var targetHash = target.Hash();
        if (source.Bytes != copy.Item.Size || target.Bytes != copy.Offset || !sourceHash.AsSpan().SequenceEqual(targetHash))
        {
            return RestartRequired();
        }

        return await CheckSourceAsync(context, copy.Item, cancellationToken);
    }

    public static FileResult RestartRequired()
    {
        var error = FileError.Invalid("The source or destination changed. Start the transfer again instead of resuming.");
        return FileResult.Failure(error);
    }

    private sealed class PrefixHashStream(long limit, IProgress<long>? progress, long initialBytes) : Stream
    {
        private readonly IncrementalHash _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        public long Bytes { get; private set; }
        public byte[] Hash() => _hash.GetHashAndReset();
        public override bool CanRead => false;
        public override bool CanWrite => true;
        public override bool CanSeek => false;
        public override long Length => Bytes;
        public override long Position { get => Bytes; set => throw new NotSupportedException(); }
        public override void Write(byte[] buffer, int offset, int count)
        {
            var span = buffer.AsSpan(offset, count);
            Write(span);
        }
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            var count = (int)Math.Min(buffer.Length, Math.Max(0, limit - Bytes));
            _hash.AppendData(buffer[..count]);
            Bytes += buffer.Length;
            progress?.Report(initialBytes + Bytes);
        }
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Write(buffer.Span);
            return ValueTask.CompletedTask;
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) _hash.Dispose();
            base.Dispose(disposing);
        }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
