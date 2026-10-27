namespace DTech.Core.Transfers.Execution;

public sealed class TransferRateMeter
{
    public static readonly TimeSpan DefaultWindow = TimeSpan.FromSeconds(5);

    private readonly Queue<RateSample> _samples = new();
    private readonly TimeSpan _window;

    public TransferRateMeter()
        : this(DefaultWindow)
    {
    }

    public TransferRateMeter(TimeSpan window)
    {
        _window = window;
    }

    public TransferRate Sample(TimeSpan elapsed, long transferredBytes, long totalBytes)
    {
        var sample = new RateSample(elapsed, transferredBytes);
        _samples.Enqueue(sample);
        while (_samples.Count > 2 && elapsed - _samples.Peek().Elapsed > _window)
        {
            _samples.Dequeue();
        }

        var oldest = _samples.Peek();
        var seconds = (elapsed - oldest.Elapsed).TotalSeconds;
        var bytes = transferredBytes - oldest.Bytes;
        if (seconds <= 0 || bytes <= 0)
        {
            return new TransferRate(0, null);
        }

        var bytesPerSecond = bytes / seconds;
        var remainingBytes = Math.Max(0, totalBytes - transferredBytes);
        var remainingSeconds = remainingBytes / bytesPerSecond;
        return new TransferRate(bytesPerSecond, remainingSeconds);
    }

    private readonly record struct RateSample(TimeSpan Elapsed, long Bytes);
}
