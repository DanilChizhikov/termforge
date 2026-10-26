using DTech.Core.Monitoring;
using DTech.Entrypoint.Monitoring;
using DTech.Entrypoint.SavedConnections;

namespace Microsoft.AspNetCore.Builder;

public static class MonitoringEndpoints
{
    public const string RoutePrefix = "/api/connections/{id:guid}/metrics";

    public static IEndpointRouteBuilder MapMonitoringEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(RoutePrefix);
        group.MapGet("/", Get);
        return endpoints;
    }

    private static async Task<IResult> Get(Guid id, ServerMonitoringService service, CancellationToken cancellationToken)
    {
        var result = await service.CollectAsync(id, cancellationToken);
        if (!result.IsSuccess)
        {
            return SavedConnectionErrorResults.ToProblem(result.Error);
        }

        var response = ServerMetricsResponse.From(result.Value);
        return Results.Ok(response);
    }
}
