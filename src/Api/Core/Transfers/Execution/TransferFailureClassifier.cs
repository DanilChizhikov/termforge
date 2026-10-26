using DTech.Core.Files;

namespace DTech.Core.Transfers.Execution;

public static class TransferFailureClassifier
{
    public static bool IsRetryable(FileErrorCode code)
    {
        return code is FileErrorCode.ConnectionFailed or FileErrorCode.Failed or FileErrorCode.Cancelled;
    }
}
