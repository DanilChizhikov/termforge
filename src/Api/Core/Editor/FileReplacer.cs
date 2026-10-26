using DTech.Core.Connections;
using DTech.Core.Files;

namespace DTech.Core.Editor;

public sealed class FileReplacer(AuxiliaryFileNames names)
{
    public const int NewFileMode = 0b110_000_000;

    private const string NotChanged = "Saving failed and the original file was not changed.";
    private const string InPlaceWarning = "The file owner could not be kept on a temporary copy, so the file was written in place.";

    public async Task<EditorResult<ReplaceOutcome>> ReplaceAsync(IRemoteEditorFileClient client, ReplaceRequest request, CancellationToken cancellationToken)
    {
        var temporaryName = names.Temporary(request.Location.Name);
        var temporaryPath = request.Location.Sibling(temporaryName);
        var mode = request.Current?.Mode ?? NewFileMode;
        var creation = new RemoteFileCreation(temporaryPath, request.Content, mode);
        var created = await client.CreateFileAsync(creation, cancellationToken);
        if (!created.IsSuccess)
        {
            return Failure(created.Error!, temporaryPath, NotChanged);
        }

        var context = new ReplaceContext(client, request, temporaryPath, []);
        if (NeedsOwnerChange(request.Current, created.Value))
        {
            var ownership = new RemoteOwnership(temporaryPath, request.Current!.UserId, request.Current.GroupId);
            var changed = await client.ChangeOwnerAsync(ownership, cancellationToken);
            if (!changed.IsSuccess)
            {
                await DeleteTemporaryAsync(context);
                return await WriteInPlaceAsync(context, cancellationToken);
            }
        }

        var rename = new RemoteRenameRequest(temporaryPath, request.Location.Path);
        var replaced = await client.ReplaceAtomicallyAsync(rename, cancellationToken);
        if (!replaced.IsSuccess)
        {
            await DeleteTemporaryAsync(context);
            return Failure(replaced.Error!, request.Location.Path, NotChanged);
        }

        if (replaced.Value == AtomicReplaceOutcome.Replaced)
        {
            return Success(SaveStrategy.AtomicRename, context);
        }

        return await ReplaceWithRenamesAsync(context, cancellationToken);
    }

    private static bool NeedsOwnerChange(RemoteFileAttributes? current, RemoteFileAttributes created)
    {
        return current is not null && (current.UserId != created.UserId || current.GroupId != created.GroupId);
    }

    private async Task<EditorResult<ReplaceOutcome>> ReplaceWithRenamesAsync(ReplaceContext context, CancellationToken cancellationToken)
    {
        var location = context.Request.Location;
        var placeTemporary = new RemoteRenameRequest(context.TemporaryPath, location.Path);
        if (context.Request.Current is null)
        {
            var placed = await context.Client.RenameAsync(placeTemporary, cancellationToken);
            if (!placed.IsSuccess)
            {
                await DeleteTemporaryAsync(context);
                return Failure(placed.Error!, location.Path, NotChanged);
            }

            return Success(SaveStrategy.RenameFallback, context);
        }

        var asideName = names.Aside(location.Name);
        var asidePath = location.Sibling(asideName);
        var moveOriginal = new RemoteRenameRequest(location.Path, asidePath);
        var moved = await context.Client.RenameAsync(moveOriginal, cancellationToken);
        if (!moved.IsSuccess)
        {
            await DeleteTemporaryAsync(context);
            return Failure(moved.Error!, location.Path, NotChanged);
        }

        var swapped = await context.Client.RenameAsync(placeTemporary, cancellationToken);
        if (!swapped.IsSuccess)
        {
            return await RestoreOriginalAsync(context, asidePath, swapped.Error!);
        }

        var removed = await context.Client.DeleteFileAsync(asidePath, CancellationToken.None);
        if (!removed.IsSuccess)
        {
            context.Warnings.Add($"The previous copy at {asidePath} could not be removed: {removed.Error!.Message}");
        }

        return Success(SaveStrategy.RenameFallback, context);
    }

    private static async Task<EditorResult<ReplaceOutcome>> RestoreOriginalAsync(ReplaceContext context, string asidePath, SshError swapError)
    {
        var location = context.Request.Location;
        var restore = new RemoteRenameRequest(asidePath, location.Path);
        var restored = await context.Client.RenameAsync(restore, CancellationToken.None);
        await DeleteTemporaryAsync(context);
        if (restored.IsSuccess)
        {
            return Failure(swapError, location.Path, NotChanged);
        }

        var stranded = $"Saving failed and the original file could not be moved back. It is preserved at {asidePath}.";
        return Failure(swapError, location.Path, stranded);
    }

    private static async Task<EditorResult<ReplaceOutcome>> WriteInPlaceAsync(ReplaceContext context, CancellationToken cancellationToken)
    {
        var location = context.Request.Location;
        var content = new RemoteFileContent(location.Path, context.Request.Content);
        var written = await context.Client.OverwriteAsync(content, cancellationToken);
        if (!written.IsSuccess)
        {
            return Failure(written.Error!, location.Path, "Writing the file in place failed. Restore it from the backup taken before this save.");
        }

        context.Warnings.Add(InPlaceWarning);
        return Success(SaveStrategy.InPlace, context);
    }

    private static async Task DeleteTemporaryAsync(ReplaceContext context)
    {
        var deleted = await context.Client.DeleteFileAsync(context.TemporaryPath, CancellationToken.None);
        if (!deleted.IsSuccess)
        {
            context.Warnings.Add($"The temporary file {context.TemporaryPath} could not be removed: {deleted.Error!.Message}");
        }
    }

    private static EditorResult<ReplaceOutcome> Success(SaveStrategy strategy, ReplaceContext context)
    {
        var outcome = new ReplaceOutcome(strategy, context.Warnings);
        return EditorResult<ReplaceOutcome>.Success(outcome);
    }

    private static EditorResult<ReplaceOutcome> Failure(SshError error, string path, string prefix)
    {
        var failure = EditorErrorMapper.FromSsh(error, path).Prefixed(prefix);
        return EditorResult<ReplaceOutcome>.Failure(failure);
    }

    private sealed record ReplaceContext(IRemoteEditorFileClient Client, ReplaceRequest Request, string TemporaryPath, List<string> Warnings);
}
