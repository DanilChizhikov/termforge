using DTech.Core.Transfers.Queue;

namespace DTech.Core.Transfers.Execution;

public sealed class TransferFileProgress(TransferJob job, long offset) : IProgress<long>
{
    public void Report(long value)
    {
        job.ReportFileBytes(offset + value);
    }
}
