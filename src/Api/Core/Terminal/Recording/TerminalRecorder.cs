using System.Text;
using System.Threading.Channels;
using DTech.Core.Terminal.Sessions;

namespace DTech.Core.Terminal.Recording;

public sealed class TerminalRecorder : ITerminalOutputObserver
{
    private const int NoRequestedEnd = -1;

    private readonly TerminalRecordingStorage _storage;
    private readonly TimeProvider _timeProvider;
    private readonly Channel<TerminalOutputChunk> _chunks;
    private readonly long _maxBytes;
    private readonly long _startTimestamp;
    private TerminalRecordingMetadata _metadata;
    private long _bytesWritten;
    private int _requestedEnd = NoRequestedEnd;
    private Task _completion = Task.CompletedTask;

    internal TerminalRecorder(TerminalRecordingStorage storage, TimeProvider timeProvider, TerminalRecorderSetup setup)
    {
        _storage = storage;
        _timeProvider = timeProvider;
        _metadata = setup.Metadata;
        _maxBytes = setup.MaxBytes;
        _startTimestamp = timeProvider.GetTimestamp();
        var channelOptions = new BoundedChannelOptions(setup.ChannelCapacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait,
        };
        _chunks = Channel.CreateBounded<TerminalOutputChunk>(channelOptions);
    }

    public TerminalRecordingId Id => _metadata.Id;

    public TerminalRecordingMetadata Metadata => Volatile.Read(ref _metadata);

    public long BytesWritten => Interlocked.Read(ref _bytesWritten);

    public long MaxBytes => _maxBytes;

    public bool IsActive => !_completion.IsCompleted;

    public Task Completion => _completion;

    public void Observe(ReadOnlySpan<byte> output)
    {
        if (Volatile.Read(ref _requestedEnd) != NoRequestedEnd)
        {
            return;
        }

        var timestamp = _timeProvider.GetTimestamp();
        var data = output.ToArray();
        var chunk = new TerminalOutputChunk(timestamp, data);
        if (!_chunks.Writer.TryWrite(chunk))
        {
            RequestStop(TerminalRecordingEndReason.WriterFellBehind);
        }
    }

    public void RequestStop(TerminalRecordingEndReason reason)
    {
        Interlocked.CompareExchange(ref _requestedEnd, (int)reason, NoRequestedEnd);
        _chunks.Writer.TryComplete();
    }

    public Task StopAsync(TerminalRecordingEndReason reason)
    {
        RequestStop(reason);
        return _completion;
    }

    internal async Task StartAsync(Stream cast, CancellationToken cancellationToken)
    {
        var header = AsciicastFormat.Header(_metadata);
        await cast.WriteAsync(header, cancellationToken);
        await cast.FlushAsync(cancellationToken);
        _bytesWritten = header.Length;
        var started = _metadata with { SizeBytes = header.Length };
        Volatile.Write(ref _metadata, started);
        await _storage.WriteMetadataAsync(started, cancellationToken);
        _completion = RunAsync(cast);
    }

    private async Task RunAsync(Stream cast)
    {
        var writerEnd = await WriteChunksAsync(cast);
        var requestedEnd = Volatile.Read(ref _requestedEnd);
        var endReason = writerEnd ?? (requestedEnd == NoRequestedEnd ? TerminalRecordingEndReason.Stopped : (TerminalRecordingEndReason)requestedEnd);
        var endedAt = _timeProvider.GetUtcNow().UtcDateTime;
        var finished = _metadata with { EndedAt = endedAt, SizeBytes = BytesWritten, EndReason = endReason };
        Volatile.Write(ref _metadata, finished);
        try
        {
            await _storage.WriteMetadataAsync(finished, CancellationToken.None);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    private async Task<TerminalRecordingEndReason?> WriteChunksAsync(Stream cast)
    {
        await Task.Yield();
        var decoder = Encoding.UTF8.GetDecoder();
        TerminalRecordingEndReason? endReason = null;
        try
        {
            await foreach (var chunk in _chunks.Reader.ReadAllAsync())
            {
                var text = Decode(decoder, chunk.Data);
                if (text.Length == 0)
                {
                    continue;
                }

                var elapsed = _timeProvider.GetElapsedTime(_startTimestamp, chunk.Timestamp);
                var line = AsciicastFormat.OutputEvent(elapsed, text);
                if (BytesWritten + line.Length > _maxBytes)
                {
                    endReason = TerminalRecordingEndReason.SizeLimitReached;
                    break;
                }

                await cast.WriteAsync(line);
                Interlocked.Add(ref _bytesWritten, line.Length);
                if (!_chunks.Reader.TryPeek(out _))
                {
                    await cast.FlushAsync();
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            endReason = TerminalRecordingEndReason.StorageFailed;
        }
        finally
        {
            var stopReason = endReason ?? TerminalRecordingEndReason.Stopped;
            RequestStop(stopReason);
            await DisposeQuietlyAsync(cast);
        }

        return endReason;
    }

    private static string Decode(Decoder decoder, byte[] data)
    {
        var characterCount = decoder.GetCharCount(data, 0, data.Length, false);
        if (characterCount == 0)
        {
            return string.Empty;
        }

        var characters = new char[characterCount];
        var decodedCount = decoder.GetChars(data, 0, data.Length, characters, 0, false);
        return new string(characters, 0, decodedCount);
    }

    private static async Task DisposeQuietlyAsync(Stream cast)
    {
        try
        {
            await cast.DisposeAsync();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }
}
