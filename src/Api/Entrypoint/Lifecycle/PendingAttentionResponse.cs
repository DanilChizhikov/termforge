using DTech.Entrypoint.Transfers;

namespace DTech.Entrypoint.Lifecycle;

internal sealed record PendingAttentionResponse(
    string State,
    IReadOnlyList<TransferResponse> Transfers,
    IReadOnlyList<UnsavedFileResponse> UnsavedFiles,
    IReadOnlyList<ActiveTerminalResponse> TerminalSessions,
    int IdleShutdownMinutes);
