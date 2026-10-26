using DTech.Core.Transfers.Queue;

namespace DTech.Core.Transfers.Execution;

public sealed record TransferFileContext(ITransferStrategy Strategy, TransferContext Context, TransferJob Job);
