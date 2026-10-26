using DTech.Core.Terminal.Recording;

namespace DTech.Core.Tests.Terminal.Recording.Support;

internal sealed class InMemoryRecordingSettingsStore : ITerminalRecordingSettingsStore
{
    public TerminalRecordingSettings? Stored { get; set; }

    public Task<TerminalRecordingSettings?> LoadAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(Stored);
    }

    public Task SaveAsync(TerminalRecordingSettings settings, CancellationToken cancellationToken)
    {
        Stored = settings;
        return Task.CompletedTask;
    }
}
