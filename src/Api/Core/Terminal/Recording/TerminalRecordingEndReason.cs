namespace DTech.Core.Terminal.Recording;

public enum TerminalRecordingEndReason
{
    Stopped,
    SessionClosed,
    RecordingDisabled,
    SizeLimitReached,
    WriterFellBehind,
    StorageFailed,
}
