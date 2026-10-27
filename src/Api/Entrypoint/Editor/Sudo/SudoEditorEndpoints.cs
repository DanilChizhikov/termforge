using DTech.Core.Editor;
using DTech.Core.Editor.Sudo;
using DTech.Entrypoint.Editor;
using DTech.Entrypoint.Editor.Sudo;

namespace Microsoft.AspNetCore.Builder;

public static class SudoEditorEndpoints
{
    public const string RoutePrefix = EditorEndpoints.RoutePrefix + "/sudo";

    public static IEndpointRouteBuilder MapSudoEditorEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(RoutePrefix);
        group.MapGet("/status", ReadStatus);
        group.MapGet("/access", ReadAccess);
        group.MapPost("/open", Open);
        group.MapPut("/file", Save);
        group.MapPost("/backups/content", ReadBackup);
        group.MapPost("/rollback", Rollback);
        group.MapDelete("/password", ForgetPassword);
        return endpoints;
    }

    private static async Task<IResult> ReadStatus(Guid connectionId, ISudoStatusReader statuses, CancellationToken cancellationToken)
    {
        var result = await statuses.ReadAsync(connectionId, cancellationToken);
        if (!result.IsSuccess)
        {
            return EditorErrorResults.ToProblem(result.Error);
        }

        var response = SudoStatusResponse.From(result.Value);
        return Results.Ok(response);
    }

    private static async Task<IResult> ReadAccess(Guid connectionId, string? path, int? ownerId, IFileAccessReader accessReader, CancellationToken cancellationToken)
    {
        if (ownerId is null)
        {
            var missing = EditorError.Invalid("The owner id is required.");
            return EditorErrorResults.ToProblem(missing);
        }

        var request = new FileAccessRequest(connectionId, path, ownerId.Value);
        var result = await accessReader.ReadAsync(request, cancellationToken);
        if (!result.IsSuccess)
        {
            return EditorErrorResults.ToProblem(result.Error);
        }

        var response = FileAccessResponse.From(result.Value);
        return Results.Ok(response);
    }

    private static async Task<IResult> Open(Guid connectionId, SudoOpenBody body, SudoFileEditor editor, CancellationToken cancellationToken)
    {
        var credentials = SudoCredentialsContract.ToCredentials(body.Sudo);
        var request = new SudoDocumentRequest(connectionId, body.Path, credentials);
        var result = await editor.OpenAsync(request, cancellationToken);
        if (!result.IsSuccess)
        {
            return EditorErrorResults.ToProblem(result.Error);
        }

        var response = EditorDocumentResponse.From(result.Value);
        return Results.Ok(response);
    }

    private static async Task<IResult> Save(Guid connectionId, SudoSaveBody body, SudoFileSaveService saves, CancellationToken cancellationToken)
    {
        var content = new TextContent(body.Content ?? string.Empty, body.HasByteOrderMark);
        var expectedVersion = body.ExpectedVersion?.ToVersion();
        var document = new SaveDocumentRequest(body.Path, content, expectedVersion);
        var credentials = SudoCredentialsContract.ToCredentials(body.Sudo);
        var request = new SudoSaveRequest(connectionId, document, credentials);
        var result = await saves.SaveAsync(request, cancellationToken);
        return EditorEndpoints.ToSaveResult(result);
    }

    private static async Task<IResult> ReadBackup(Guid connectionId, SudoReadBackupBody body, SudoFileEditor editor, CancellationToken cancellationToken)
    {
        var reference = new BackupReference(body.Path, body.Name);
        var credentials = SudoCredentialsContract.ToCredentials(body.Sudo);
        var request = new SudoBackupRequest(connectionId, reference, credentials);
        var result = await editor.ReadBackupAsync(request, cancellationToken);
        if (!result.IsSuccess)
        {
            return EditorErrorResults.ToProblem(result.Error);
        }

        var response = EditorDocumentResponse.From(result.Value);
        return Results.Ok(response);
    }

    private static async Task<IResult> Rollback(Guid connectionId, SudoRollbackBody body, SudoFileSaveService saves, CancellationToken cancellationToken)
    {
        var expectedVersion = body.ExpectedVersion?.ToVersion();
        var document = new RollbackDocumentRequest(body.Path, body.BackupName, expectedVersion);
        var credentials = SudoCredentialsContract.ToCredentials(body.Sudo);
        var request = new SudoRollbackRequest(connectionId, document, credentials);
        var result = await saves.RollbackAsync(request, cancellationToken);
        return EditorEndpoints.ToSaveResult(result);
    }

    private static async Task<IResult> ForgetPassword(Guid connectionId, SudoPasswordStore store, CancellationToken cancellationToken)
    {
        var failure = await store.DeleteAsync(connectionId, cancellationToken);
        return failure is null ? Results.NoContent() : EditorErrorResults.ToProblem(failure);
    }
}
