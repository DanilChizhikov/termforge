namespace DTech.Core.Terminal.Recording;

public sealed class TerminalRecorderFactory(TerminalRecordingStorage storage, TimeProvider timeProvider)
{
    public async Task<TerminalRecordingResult<TerminalRecorder>> StartAsync(TerminalRecorderSetup setup, CancellationToken cancellationToken)
    {
        var recorder = new TerminalRecorder(storage, timeProvider, setup);
        Stream? cast = null;
        var started = false;
        try
        {
            cast = storage.CreateCast(setup.Metadata.Id);
            await recorder.StartAsync(cast, cancellationToken);
            started = true;
            return TerminalRecordingResult<TerminalRecorder>.Success(recorder);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            var failure = TerminalRecordingError.StorageFailed($"The recording could not be created in {storage.DirectoryPath}: {exception.Message}");
            return TerminalRecordingResult<TerminalRecorder>.Failure(failure);
        }
        finally
        {
            if (!started && cast is not null)
            {
                try
                {
                    await cast.DisposeAsync();
                }
                finally
                {
                    storage.Delete(setup.Metadata.Id);
                }
            }
        }
    }

    public async Task DiscardAsync(TerminalRecorder recorder)
    {
        try
        {
            await recorder.StopAsync(TerminalRecordingEndReason.Stopped);
        }
        finally
        {
            storage.Delete(recorder.Id);
        }
    }
}
