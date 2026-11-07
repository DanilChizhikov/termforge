using DTech.Core.Transfers.Queue;

namespace DTech.Core.Transfers.Execution;

internal sealed class TransferVerificationProgress(TransferJob job) : IProgress<long>
{
    public void Report(long value) => job.ReportVerification(value);
}
