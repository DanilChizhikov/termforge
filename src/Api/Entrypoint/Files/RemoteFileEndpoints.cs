using DTech.Core.Files;
using DTech.Core.Files.Remote;
using DTech.Entrypoint.Files;

namespace Microsoft.AspNetCore.Builder;

public static class RemoteFileEndpoints
{
    public const string RoutePrefix = "/api/files/remote/{connectionId:guid}";

    public static IEndpointRouteBuilder MapRemoteFileEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(RoutePrefix);
        group.MapGet("/list", List);
        group.MapGet("/stat", Stat);
        group.MapPost("/directories", CreateDirectory);
        group.MapPost("/rename", Rename);
        group.MapPost("/delete", Delete);
        return endpoints;
    }

    private static async Task<IResult> List(Guid connectionId, string? path, RemoteFileBrowser browser, CancellationToken cancellationToken)
    {
        var result = await browser.ListAsync(connectionId, path, cancellationToken);
        if (!result.IsSuccess)
        {
            return FileErrorResults.ToProblem(result.Error);
        }

        var response = DirectoryListingResponse.From(result.Value);
        return Results.Ok(response);
    }

    private static async Task<IResult> Stat(Guid connectionId, string? path, RemoteFileBrowser browser, CancellationToken cancellationToken)
    {
        var result = await browser.StatAsync(connectionId, path, cancellationToken);
        if (!result.IsSuccess)
        {
            return FileErrorResults.ToProblem(result.Error);
        }

        var response = FileEntryResponse.From(result.Value);
        return Results.Ok(response);
    }

    private static async Task<IResult> CreateDirectory(Guid connectionId, CreateDirectoryBody body, RemoteFileOperations operations, CancellationToken cancellationToken)
    {
        var request = new CreateDirectoryRequest(body.ParentPath ?? string.Empty, body.Name ?? string.Empty);
        var result = await operations.CreateDirectoryAsync(connectionId, request, cancellationToken);
        return ToNoContent(result);
    }

    private static async Task<IResult> Rename(Guid connectionId, RenameBody body, RemoteFileOperations operations, CancellationToken cancellationToken)
    {
        var request = new RenameRequest(body.Path ?? string.Empty, body.NewName ?? string.Empty);
        var result = await operations.RenameAsync(connectionId, request, cancellationToken);
        return ToNoContent(result);
    }

    private static async Task<IResult> Delete(Guid connectionId, DeleteBody body, RemoteFileOperations operations, CancellationToken cancellationToken)
    {
        var request = new DeleteRequest(body.Path ?? string.Empty, body.Confirm);
        var result = await operations.DeleteAsync(connectionId, request, cancellationToken);
        return ToNoContent(result);
    }

    private static IResult ToNoContent(FileResult result)
    {
        return result.IsSuccess ? Results.NoContent() : FileErrorResults.ToProblem(result.Error);
    }
}
