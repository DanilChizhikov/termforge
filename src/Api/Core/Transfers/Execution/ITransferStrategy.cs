using DTech.Core.Files;

namespace DTech.Core.Transfers.Execution;

public interface ITransferStrategy
{
    TransferDirection Direction { get; }

    Task<FileResult<TransferInspection>> InspectAsync(TransferContext context, CancellationToken cancellationToken);

    Task<FileResult<TransferPlan>> PlanAsync(TransferContext context, CancellationToken cancellationToken);

    Task<FileResult> CreateDirectoryAsync(TransferContext context, string targetPath, CancellationToken cancellationToken);

    Task<FileResult<long?>> FindTargetSizeAsync(TransferContext context, string targetSidePath, CancellationToken cancellationToken);

    string GetPartialPath(TransferItem item, string partialName);

    Task<FileResult> CopyAsync(TransferContext context, TransferCopy copy, CancellationToken cancellationToken);

    Task<FileResult> CommitAsync(TransferContext context, TransferCopy copy, CancellationToken cancellationToken);

    Task<FileResult> DeletePartialAsync(TransferContext context, string partialPath, CancellationToken cancellationToken);
}
