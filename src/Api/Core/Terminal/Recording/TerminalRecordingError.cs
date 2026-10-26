namespace DTech.Core.Terminal.Recording;

public sealed record TerminalRecordingError(TerminalRecordingErrorCode Code, string Message)
{
    public static TerminalRecordingError InvalidSettings(string message)
    {
        return new TerminalRecordingError(TerminalRecordingErrorCode.InvalidSettings, message);
    }

    public static TerminalRecordingError InvalidId()
    {
        return new TerminalRecordingError(TerminalRecordingErrorCode.InvalidId, "The recording id is invalid.");
    }

    public static TerminalRecordingError NotFound()
    {
        return new TerminalRecordingError(TerminalRecordingErrorCode.NotFound, "The recording does not exist.");
    }

    public static TerminalRecordingError Disabled()
    {
        return new TerminalRecordingError(TerminalRecordingErrorCode.Disabled, "Terminal recording is turned off. Turn on \"Record terminal output\" in Settings first.");
    }

    public static TerminalRecordingError AlreadyRecording()
    {
        return new TerminalRecordingError(TerminalRecordingErrorCode.AlreadyRecording, "This terminal is already being recorded.");
    }

    public static TerminalRecordingError NotRecording()
    {
        return new TerminalRecordingError(TerminalRecordingErrorCode.NotRecording, "This terminal is not being recorded.");
    }

    public static TerminalRecordingError StillRecording()
    {
        return new TerminalRecordingError(TerminalRecordingErrorCode.StillRecording, "Stop the recording before deleting it.");
    }

    public static TerminalRecordingError StorageFailed(string message)
    {
        return new TerminalRecordingError(TerminalRecordingErrorCode.StorageFailed, message);
    }
}
