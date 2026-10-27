using DTech.Core.Editor;
using DTech.Entrypoint.Editor;

namespace Microsoft.AspNetCore.Builder;

public static class EditorEndpoints
{
    public const string RoutePrefix = "/api/editor/remote/{connectionId:guid}";

    public static IEndpointRouteBuilder MapEditorEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(RoutePrefix);
        group.MapGet("/file", Open);
        group.MapPut("/file", Save);
        group.MapGet("/backups", ListBackups);
        group.MapGet("/backups/content", ReadBackup);
        group.MapPost("/rollback", Rollback);
        return endpoints;
    }

    private static async Task<IResult> Open(Guid connectionId, string? path, RemoteFileEditor editor, CancellationToken cancellationToken)
    {
        var result = await editor.OpenAsync(connectionId, path, cancellationToken);
        if (!result.IsSuccess)
        {
            return EditorErrorResults.ToProblem(result.Error);
        }

        var response = EditorDocumentResponse.From(result.Value);
        return Results.Ok(response);
    }

    private static async Task<IResult> Save(Guid connectionId, SaveFileBody body, RemoteFileSaveService saves, CancellationToken cancellationToken)
    {
        var content = new TextContent(body.Content ?? string.Empty, body.HasByteOrderMark);
        var expectedVersion = body.ExpectedVersion?.ToVersion();
        var request = new SaveDocumentRequest(body.Path, content, expectedVersion);
        var result = await saves.SaveAsync(connectionId, request, cancellationToken);
        return ToSaveResult(result);
    }

    private static async Task<IResult> ListBackups(Guid connectionId, string? path, RemoteFileEditor editor, CancellationToken cancellationToken)
    {
        var result = await editor.ListBackupsAsync(connectionId, path, cancellationToken);
        if (!result.IsSuccess)
        {
            return EditorErrorResults.ToProblem(result.Error);
        }

        var backups = result.Value;
        var response = new FileBackupResponse[backups.Count];
        for (var i = 0; i < backups.Count; i++)
        {
            var backup = backups[i];
            response[i] = FileBackupResponse.From(backup);
        }

        return Results.Ok(response);
    }

    private static async Task<IResult> ReadBackup(Guid connectionId, string? path, string? name, RemoteFileEditor editor, CancellationToken cancellationToken)
    {
        var reference = new BackupReference(path, name);
        var result = await editor.ReadBackupAsync(connectionId, reference, cancellationToken);
        if (!result.IsSuccess)
        {
            return EditorErrorResults.ToProblem(result.Error);
        }

        var response = EditorDocumentResponse.From(result.Value);
        return Results.Ok(response);
    }

    private static async Task<IResult> Rollback(Guid connectionId, RollbackBody body, RemoteFileSaveService saves, CancellationToken cancellationToken)
    {
        var expectedVersion = body.ExpectedVersion?.ToVersion();
        var request = new RollbackDocumentRequest(body.Path, body.BackupName, expectedVersion);
        var result = await saves.RollbackAsync(connectionId, request, cancellationToken);
        return ToSaveResult(result);
    }

    internal static IResult ToSaveResult(EditorResult<JournaledSave> result)
    {
        if (!result.IsSuccess)
        {
            return EditorErrorResults.ToProblem(result.Error);
        }

        if (!result.Value.Result.IsSaved)
        {
            var conflict = ConflictResponse.From(result.Value);
            return Results.Conflict(conflict);
        }

        var saved = SavedFileResponse.From(result.Value);
        return Results.Ok(saved);
    }
}
