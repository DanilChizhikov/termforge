using DTech.Core.Diagnostics;
using DTech.Core.SavedConnections;
using DTech.Entrypoint.Diagnostics;
using DTech.Entrypoint.SavedConnections;

namespace Microsoft.AspNetCore.Builder;

public static class DiagnosticsEndpoints
{
    public const string RoutePrefix = "/api/connections/{id:guid}/capabilities";

    public static IEndpointRouteBuilder MapDiagnosticsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(RoutePrefix);
        group.MapGet("/", Get);
        group.MapPost("/refresh", Refresh);
        return endpoints;
    }

    private static async Task<IResult> Get(Guid id, CapabilityDiagnosticsService service, CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(id, cancellationToken);
        return ToResponse(result);
    }

    private static async Task<IResult> Refresh(Guid id, CapabilityDiagnosticsService service, CancellationToken cancellationToken)
    {
        var result = await service.RefreshAsync(id, cancellationToken);
        return ToResponse(result);
    }

    private static IResult ToResponse(SavedConnectionResult<CapabilityReport> result)
    {
        if (!result.IsSuccess)
        {
            return SavedConnectionErrorResults.ToProblem(result.Error);
        }

        var response = CapabilityReportResponse.From(result.Value);
        return Results.Ok(response);
    }
}
