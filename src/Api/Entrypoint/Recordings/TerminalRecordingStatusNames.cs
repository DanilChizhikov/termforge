using DTech.Core.Terminal.Recording;

namespace DTech.Entrypoint.Recordings;

internal static class TerminalRecordingStatusNames
{
    public const string Recording = "recording";
    public const string Complete = "complete";
    public const string Truncated = "truncated";
    public const string Interrupted = "interrupted";

    public static string From(TerminalRecordingMetadata metadata, bool isActive)
    {
        if (isActive)
        {
            return Recording;
        }

        if (metadata.EndReason is null)
        {
            return Interrupted;
        }

        return metadata.IsTruncated ? Truncated : Complete;
    }

    public static string? EndReasonName(TerminalRecordingEndReason? reason)
    {
        return reason switch
        {
            TerminalRecordingEndReason.Stopped => "stopped",
            TerminalRecordingEndReason.SessionClosed => "sessionClosed",
            TerminalRecordingEndReason.RecordingDisabled => "recordingDisabled",
            TerminalRecordingEndReason.SizeLimitReached => "sizeLimitReached",
            TerminalRecordingEndReason.WriterFellBehind => "writerFellBehind",
            TerminalRecordingEndReason.StorageFailed => "storageFailed",
            _ => null,
        };
    }
}
