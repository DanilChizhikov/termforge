using DTech.Core.Terminal.Recording;

namespace DTech.Entrypoint.Recordings;

internal sealed record SessionRecordingResponse(bool Recording, bool Available)
{
    public string? RecordingId { get; init; }

    public long SizeBytes { get; init; }

    public long MaxBytes { get; init; }

    public string? Status { get; init; }

    public string? EndReason { get; init; }

    public static SessionRecordingResponse From(TerminalRecorder? recorder, bool isAvailable)
    {
        if (recorder is null)
        {
            return new SessionRecordingResponse(false, isAvailable);
        }

        var metadata = recorder.Metadata;
        var isActive = recorder.IsActive;
        return new SessionRecordingResponse(isActive, isAvailable)
        {
            RecordingId = metadata.Id.Value,
            SizeBytes = recorder.BytesWritten,
            MaxBytes = recorder.MaxBytes,
            Status = TerminalRecordingStatusNames.From(metadata, isActive),
            EndReason = TerminalRecordingStatusNames.EndReasonName(metadata.EndReason),
        };
    }
}
