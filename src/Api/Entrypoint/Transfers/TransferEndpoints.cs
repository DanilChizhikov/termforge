using DTech.Core.SavedConnections;
using DTech.Core.Transfers;
using DTech.Core.Transfers.Queue;
using DTech.Entrypoint.Files;
using DTech.Entrypoint.Transfers;

namespace Microsoft.AspNetCore.Builder;

public static class TransferEndpoints
{
    public const string RoutePrefix = "/api/transfers";

    public static IEndpointRouteBuilder MapTransferEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(RoutePrefix);
        group.MapGet("/", List);
        group.MapPost("/", Create);
        group.MapPost("/{id:guid}/cancel", Cancel);
        group.MapPost("/{id:guid}/discard", Discard);
        group.MapPost("/clear", Clear);
        return endpoints;
    }

    private static IResult List(TransferControl control)
    {
        var snapshots = control.List();
        var responses = ToResponses(snapshots);
        return Results.Ok(responses);
    }

    private static async Task<IResult> Create(CreateTransfersBody body, ISavedConnectionRepository connections, TransferEnqueuer enqueuer, CancellationToken cancellationToken)
    {
        if (body.ConnectionId is not { } connectionId || !Enum.TryParse<TransferDirection>(body.Direction, true, out var direction) || string.IsNullOrWhiteSpace(body.TargetDirectory))
        {
            return TransferErrorResults.Invalid("connectionId, direction (upload or download) and targetDirectory are required.");
        }

        var connection = await connections.FindAsync(connectionId, cancellationToken);
        if (connection is null)
        {
            return TransferErrorResults.Invalid($"Saved connection {connectionId} was not found.");
        }

        var batch = new TransferBatchRequest
        {
            ConnectionId = connectionId,
            ServerName = connection.Details.Name,
            Direction = direction,
            SourcePaths = body.SourcePaths ?? [],
            TargetDirectory = body.TargetDirectory,
            Overwrite = body.Overwrite,
        };
        var result = await enqueuer.EnqueueAsync(batch, cancellationToken);
        if (result.Error is not null)
        {
            return FileErrorResults.ToProblem(result.Error);
        }

        if (result.Conflicts.Count > 0)
        {
            return TransferErrorResults.Conflict(result.Conflicts);
        }

        var responses = ToResponses(result.Transfers);
        return Results.Accepted(RoutePrefix, responses);
    }

    private static async Task<IResult> Cancel(Guid id, TransferControl control)
    {
        var outcome = await control.CancelAsync(id);
        return outcome == TransferCancelOutcome.NotFound ? TransferErrorResults.NotFound(id) : Results.Accepted();
    }

    private static async Task<IResult> Discard(Guid id, TransferControl control, CancellationToken cancellationToken)
    {
        var result = await control.DiscardAsync(id, cancellationToken);
        return result.IsSuccess ? Results.NoContent() : FileErrorResults.ToProblem(result.Error);
    }

    private static async Task<IResult> Clear(TransferControl control)
    {
        await control.ClearFinishedAsync();
        return Results.NoContent();
    }

    private static List<TransferResponse> ToResponses(IReadOnlyList<TransferSnapshot> snapshots)
    {
        var responses = new List<TransferResponse>(snapshots.Count);
        for (var i = 0; i < snapshots.Count; i++)
        {
            var response = TransferResponse.From(snapshots[i]);
            responses.Add(response);
        }

        return responses;
    }
}
