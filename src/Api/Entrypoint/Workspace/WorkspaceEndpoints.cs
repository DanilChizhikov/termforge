using DTech.Core.Workspace;
using DTech.Entrypoint.Workspace;
using Microsoft.AspNetCore.Mvc;

namespace Microsoft.AspNetCore.Builder;

public static class WorkspaceEndpoints
{
    public const string Route = "/api/workspace";

    public static IEndpointRouteBuilder MapWorkspaceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var workspace = endpoints.MapGroup(Route);
        workspace.MapGet("/", Get);
        workspace.MapPut("/", Save);
        return endpoints;
    }

    private static async Task<IResult> Get(WorkspaceStateLoader loader, ILogger<WorkspaceStateLoader> logger, CancellationToken cancellationToken)
    {
        var restore = await loader.LoadAsync(cancellationToken);
        if (restore.Warning is not null)
        {
            logger.LogWarning($"Saved workspace state was reset: {restore.Warning.Code}");
        }

        var response = WorkspaceRestoreResponse.From(restore);
        return Results.Ok(response);
    }

    private static async Task<IResult> Save([FromBody] WorkspaceSnapshot? snapshot, WorkspaceStateSaver saver, CancellationToken cancellationToken)
    {
        var saved = await saver.SaveAsync(snapshot, cancellationToken);
        if (!saved.IsSuccess)
        {
            return Results.Problem(detail: saved.Error.Message, statusCode: StatusCodes.Status400BadRequest);
        }

        return Results.NoContent();
    }
}
