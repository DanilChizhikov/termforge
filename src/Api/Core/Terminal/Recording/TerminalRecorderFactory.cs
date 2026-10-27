namespace DTech.Core.Terminal.Recording;

public sealed class TerminalRecorderFactory(TerminalRecordingStorage storage, TimeProvider timeProvider)
{
    public async Task<TerminalRecordingResult<TerminalRecorder>> StartAsync(TerminalRecorderSetup setup, CancellationToken cancellationToken)
    {
        var recorder = new TerminalRecorder(storage, timeProvider, setup);
        Stream? cast = null;
        try
        {
            cast = storage.CreateCast(setup.Metadata.Id);
            await recorder.StartAsync(cast, cancellationToken);
            return TerminalRecordingResult<TerminalRecorder>.Success(recorder);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            if (cast is not null)
            {
                await cast.DisposeAsync();
            }

            storage.Delete(setup.Metadata.Id);
            var failure = TerminalRecordingError.StorageFailed($"The recording could not be created in {storage.DirectoryPath}: {exception.Message}");
            return TerminalRecordingResult<TerminalRecorder>.Failure(failure);
        }
    }

    public async Task DiscardAsync(TerminalRecorder recorder)
    {
        await recorder.StopAsync(TerminalRecordingEndReason.Stopped);
        storage.Delete(recorder.Id);
    }
}
