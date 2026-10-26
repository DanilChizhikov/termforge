using DTech.Core.Files;
using DTech.Core.Files.Local;
using DTech.Entrypoint.Files;

namespace Microsoft.AspNetCore.Builder;

public static class LocalFileEndpoints
{
    public const string RoutePrefix = "/api/files/local";

    public static IEndpointRouteBuilder MapLocalFileEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(RoutePrefix);
        group.MapGet("/roots", GetRoots);
        group.MapGet("/list", List);
        group.MapGet("/stat", Stat);
        group.MapPost("/directories", CreateDirectory);
        group.MapPost("/rename", Rename);
        group.MapPost("/delete", Delete);
        return endpoints;
    }

    private static IResult GetRoots(LocalFileBrowser browser)
    {
        var roots = browser.GetRoots();
        var responses = new List<LocalRootResponse>(roots.Count);
        for (var i = 0; i < roots.Count; i++)
        {
            var response = LocalRootResponse.From(roots[i]);
            responses.Add(response);
        }

        return Results.Ok(responses);
    }

    private static IResult List(string? path, LocalFileBrowser browser)
    {
        var result = browser.List(path);
        if (!result.IsSuccess)
        {
            return FileErrorResults.ToProblem(result.Error);
        }

        var response = DirectoryListingResponse.From(result.Value);
        return Results.Ok(response);
    }

    private static IResult Stat(string? path, LocalFileBrowser browser)
    {
        var result = browser.Stat(path);
        if (!result.IsSuccess)
        {
            return FileErrorResults.ToProblem(result.Error);
        }

        var response = FileEntryResponse.From(result.Value);
        return Results.Ok(response);
    }

    private static IResult CreateDirectory(CreateDirectoryBody body, LocalFileOperations operations)
    {
        var request = new CreateDirectoryRequest(body.ParentPath ?? string.Empty, body.Name ?? string.Empty);
        var result = operations.CreateDirectory(request);
        return ToNoContent(result);
    }

    private static IResult Rename(RenameBody body, LocalFileOperations operations)
    {
        var request = new RenameRequest(body.Path ?? string.Empty, body.NewName ?? string.Empty);
        var result = operations.Rename(request);
        return ToNoContent(result);
    }

    private static IResult Delete(DeleteBody body, LocalFileOperations operations)
    {
        var request = new DeleteRequest(body.Path ?? string.Empty, body.Confirm);
        var result = operations.Delete(request);
        return ToNoContent(result);
    }

    private static IResult ToNoContent(FileResult result)
    {
        return result.IsSuccess ? Results.NoContent() : FileErrorResults.ToProblem(result.Error);
    }
}
