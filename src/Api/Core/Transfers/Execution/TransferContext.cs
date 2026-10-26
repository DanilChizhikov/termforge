using DTech.Core.Files;

namespace DTech.Core.Transfers.Execution;

public sealed record TransferContext(IRemoteFileClient Client, TransferRequest Request);
