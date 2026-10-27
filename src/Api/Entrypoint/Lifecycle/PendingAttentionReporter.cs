using DTech.Core.Operations;
using DTech.Core.Terminal.Sessions;
using DTech.Core.Transfers;
using DTech.Entrypoint.Transfers;
using Microsoft.Extensions.Options;

namespace DTech.Entrypoint.Lifecycle;

internal sealed class PendingAttentionReporter
{
    private readonly TransferControl _transfers;
    private readonly TerminalSessionManager _sessions;
    private readonly UnsavedFileReporter _unsavedFiles;
    private readonly LifecycleOptions _options;

    public PendingAttentionReporter(TransferControl transfers, TerminalSessionManager sessions, UnsavedFileReporter unsavedFiles, IOptions<LifecycleOptions> options)
    {
        _transfers = transfers;
        _sessions = sessions;
        _unsavedFiles = unsavedFiles;
        _options = options.Value;
    }

    public async Task<PendingAttentionResponse> ReportAsync(ApplicationExitState state, CancellationToken cancellationToken)
    {
        var activeTransfers = ListActiveTransfers();
        var unsaved = await _unsavedFiles.ListAsync(cancellationToken);
        var terminals = ListTerminals();
        var stateName = ExitStateNames.Of(state);
        return new PendingAttentionResponse(stateName, activeTransfers, unsaved, terminals, _options.IdleShutdownMinutes);
    }

    private List<TransferResponse> ListActiveTransfers()
    {
        var snapshots = _transfers.List();
        var active = new List<TransferResponse>();
        for (var i = 0; i < snapshots.Count; i++)
        {
            var snapshot = snapshots[i];
            if (OperationStateMachine.IsTerminal(snapshot.Status))
            {
                continue;
            }

            var response = TransferResponse.From(snapshot);
            active.Add(response);
        }

        return active;
    }

    private List<ActiveTerminalResponse> ListTerminals()
    {
        var descriptors = _sessions.ListAll();
        var terminals = new List<ActiveTerminalResponse>(descriptors.Count);
        for (var i = 0; i < descriptors.Count; i++)
        {
            var terminal = ActiveTerminalResponse.From(descriptors[i]);
            terminals.Add(terminal);
        }

        return terminals;
    }
}
