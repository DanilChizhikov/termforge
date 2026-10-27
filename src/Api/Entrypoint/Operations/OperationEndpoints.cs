using DTech.Core.Operations;
using DTech.Entrypoint.Operations;

namespace Microsoft.AspNetCore.Builder;

public static class OperationEndpoints
{
    public const string RoutePrefix = "/api/operations";

    public static IEndpointRouteBuilder MapOperationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(RoutePrefix);
        group.MapGet("/", List);
        group.MapGet("/{id:guid}", Get);
        group.MapPost("/{id:guid}/retry", Retry);
        return endpoints;
    }

    private static async Task<IResult> List([AsParameters] OperationListRequest request, IOperationJournal journal, CancellationToken cancellationToken)
    {
        OperationStatus? status = null;
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (!Enum.TryParse<OperationStatus>(request.Status, true, out var parsed))
            {
                return OperationErrorResults.InvalidStatus(request.Status);
            }

            status = parsed;
        }

        var query = new OperationQuery(
            request.Kind,
            status,
            request.Server,
            request.From?.UtcDateTime,
            request.To?.UtcDateTime,
            request.Offset ?? 0,
            request.Limit ?? OperationQuery.DefaultLimit);
        var page = await journal.List(query, cancellationToken);
        var items = new List<OperationResponse>(page.Items.Count);
        for (var i = 0; i < page.Items.Count; i++)
        {
            var item = OperationResponse.From(page.Items[i]);
            items.Add(item);
        }

        var response = new OperationPageResponse(items, page.Total, page.Offset, page.Limit);
        return Results.Ok(response);
    }

    private static async Task<IResult> Get(Guid id, IOperationJournal journal, CancellationToken cancellationToken)
    {
        var operation = await journal.Find(id, cancellationToken);
        if (operation is null)
        {
            var notFound = new OperationException(OperationFailure.NotFound, "the operation was not found.");
            return OperationErrorResults.ToProblem(notFound);
        }

        var response = OperationResponse.From(operation);
        return Results.Ok(response);
    }

    private static async Task<IResult> Retry(Guid id, IOperationJournal journal, CancellationToken cancellationToken)
    {
        try
        {
            await journal.Retry(id, cancellationToken);
        }
        catch (OperationException exception)
        {
            return OperationErrorResults.ToProblem(exception);
        }

        return Results.Accepted();
    }
}
