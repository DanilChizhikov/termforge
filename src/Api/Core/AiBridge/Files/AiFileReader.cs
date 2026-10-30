using DTech.Core.Editor;

namespace DTech.Core.AiBridge.Files;

public sealed class AiFileReader(AiServerAccessPolicy accessPolicy, RemoteFileEditor editor)
{
    public async Task<AiBridgeResult<RemoteDocument>> ReadAsync(Guid connectionId, string? path, CancellationToken cancellationToken)
    {
        var access = await accessPolicy.CheckAsync(connectionId, cancellationToken);
        if (!access.IsSuccess)
        {
            return AiBridgeResult<RemoteDocument>.Failure(access.Error);
        }

        var opened = await editor.OpenAsync(connectionId, path, cancellationToken);
        if (!opened.IsSuccess)
        {
            var failure = AiBridgeErrorMapper.FromEditor(opened.Error);
            return AiBridgeResult<RemoteDocument>.Failure(failure);
        }

        return AiBridgeResult<RemoteDocument>.Success(opened.Value);
    }
}
