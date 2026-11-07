using DTech.Core.Updates;
using DTech.Entrypoint.Updates;
using DTech.Entrypoint.Updates.Installation;
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
        group.MapGet("/installation", (UpdateInstaller installer) => installer.Status());
        group.MapPost("/prepare", (UpdateInstaller installer) => Run(() => installer.Prepare(false)));
        group.MapPost("/rollback", (UpdateInstaller installer) => Run(() => installer.Prepare(true)));
        group.MapPost("/cancel", (UpdateInstaller installer) => Run(installer.Cancel));
        group.MapPost("/install", Install);
        return endpoints;
    }

    private static IResult Install(InstallUpdateRequest request, UpdateInstaller installer, HttpContext context)
    {
        var result = Run(() =>
        {
            if (request.WaitForTransfers && !installer.TransfersFinished) installer.WaitForTransfers();
            else
            {
                var rollback = new RollbackUpdateRequest(request.SaveCurrentData);
                installer.Commit(context, rollback);
            }
        });
        return result is Microsoft.AspNetCore.Http.HttpResults.Accepted
            ? Results.Json(installer.Status(), statusCode: StatusCodes.Status202Accepted)
            : result;
    }

    private static IResult Run(Action action)
    {
        try
        {
            action();
            return Results.Accepted();
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            return Results.Problem(detail: exception.Message, statusCode: StatusCodes.Status409Conflict);
        }
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
