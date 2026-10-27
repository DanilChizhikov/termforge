using DTech.Core.Updates;
using DTech.Entrypoint.Updates;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.Builder;

public static class UpdateEndpoints
{
    public const string RoutePrefix = "/api/updates";

    public static IEndpointRouteBuilder MapUpdateEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(RoutePrefix);
        group.MapGet("/", GetStatus);
        group.MapPost("/check", CheckNow);
        return endpoints;
    }

    private static async Task<IResult> GetStatus(IUpdateChecker checker, IOptions<UpdateOptions> options, CancellationToken cancellationToken)
    {
        var status = options.Value.CheckOnStartup
            ? await checker.Check(false, cancellationToken)
            : checker.Peek();
        var response = UpdateStatusResponse.From(status);
        return Results.Ok(response);
    }

    private static async Task<IResult> CheckNow(IUpdateChecker checker, CancellationToken cancellationToken)
    {
        var status = await checker.Check(true, cancellationToken);
        var response = UpdateStatusResponse.From(status);
        return Results.Ok(response);
    }
}
