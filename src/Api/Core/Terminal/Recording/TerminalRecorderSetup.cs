namespace DTech.Core.Terminal.Recording;

public sealed record TerminalRecorderSetup(TerminalRecordingMetadata Metadata, long MaxBytes)
{
    public int ChannelCapacity { get; init; } = TerminalRecordingLimits.OutputChannelCapacity;
}
