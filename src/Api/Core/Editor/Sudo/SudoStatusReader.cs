namespace DTech.Core.Editor.Sudo;

public sealed class SudoStatusReader(RemoteShellConnector connector, SudoAvailabilityProbe probe, SudoPasswordStore store) : ISudoStatusReader
{
    private const string Subject = "sudo";

    public async Task<EditorResult<SudoStatus>> ReadAsync(Guid connectionId, CancellationToken cancellationToken)
    {
        var connected = await connector.ConnectAsync(connectionId, cancellationToken);
        if (!connected.IsSuccess)
        {
            return EditorResult<SudoStatus>.Failure(connected.Error);
        }

        await using var runner = connected.Value.Runner;
        var probed = await probe.ProbeAsync(runner, cancellationToken);
        if (!probed.IsSuccess)
        {
            var failure = EditorErrorMapper.FromSsh(probed.Error!, Subject);
            return EditorResult<SudoStatus>.Failure(failure);
        }

        var saved = await store.ReadAsync(connectionId, cancellationToken);
        using var savedPassword = saved.IsSuccess ? saved.Value : null;
        var outcome = probed.Value;
        var status = new SudoStatus(outcome.Availability, savedPassword is not null, outcome.Message);
        return EditorResult<SudoStatus>.Success(status);
    }
}
