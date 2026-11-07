using DTech.Core.NetworkDiagnostics;
using DTech.Entrypoint.NetworkDiagnostics;
using DTech.Entrypoint.Realtime;

namespace Microsoft.AspNetCore.Builder;

public static class NetworkDiagnosticEndpoints
{
    private const string Prefix = "/api/network-diagnostics";

    public static IEndpointRouteBuilder MapNetworkDiagnosticEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(Prefix);
        group.MapPost("/prepare", Prepare);
        group.MapPost("/", Start);
        group.MapGet("/", (NetworkDiagnosticStore store) => Results.Ok(store.List()));
        group.MapGet("/{id:guid}", Get);
        group.MapGet("/{id:guid}/results", Page);
        group.MapPost("/{id:guid}/cancel", Cancel);
        group.MapPost("/remote-actions/prepare", PrepareAction);
        group.MapPost("/remote-actions/execute", ExecuteAction);
        return endpoints;
    }

    private static async Task<IResult> Prepare(NetworkDiagnosticRequest request, NetworkDiagnosticService service, CancellationToken cancellationToken)
    {
        try { return Results.Ok(await service.PrepareAsync(request, cancellationToken)); }
        catch (Exception exception) when (IsRequestError(exception)) { return Problem(exception); }
    }

    private static async Task<IResult> Start(NetworkDiagnosticRequest request, NetworkDiagnosticService service, CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = await service.StartAsync(request, cancellationToken);
            return Results.Accepted($"{Prefix}/{snapshot.Id}", snapshot);
        }
        catch (Exception exception) when (IsRequestError(exception)) { return Problem(exception); }
    }

    private static IResult Get(Guid id, NetworkDiagnosticStore store)
    {
        var run = store.Find(id);
        return run is null ? Results.NotFound() : Results.Ok(run.Snapshot());
    }

    private static IResult Page(Guid id, HttpContext context, NetworkDiagnosticStore store)
    {
        var offsetText = context.Request.Query["offset"].FirstOrDefault() ?? "0";
        var limitText = context.Request.Query["limit"].FirstOrDefault() ?? "100";
        if (!int.TryParse(offsetText, out var offset) || offset < 0 || !int.TryParse(limitText, out var limit) || limit is < 1 or > 1000)
            return Results.BadRequest("Use a non-negative offset and a limit between 1 and 1000.");
        var run = store.Find(id);
        return run is null ? Results.NotFound() : Results.Ok(run.Page(offset, limit));
    }

    private static async Task<IResult> Cancel(Guid id, NetworkDiagnosticStore store, RealtimeChannel channel)
    {
        var run = store.Find(id);
        if (run is null) return Results.NotFound();
        run.Cancel();
        var snapshot = run.Snapshot();
        await channel.Publish("network-diagnostics.changed", snapshot, CancellationToken.None);
        return Results.Accepted();
    }

    private static async Task<IResult> PrepareAction(RemoteNetworkActionRequest request, RemoteNetworkActionService service, CancellationToken cancellationToken)
    {
        try { return Results.Ok(await service.PrepareAsync(request, cancellationToken)); }
        catch (Exception exception) when (IsRequestError(exception)) { return Problem(exception); }
    }

    private static async Task<IResult> ExecuteAction(RemoteNetworkActionExecution request, RemoteNetworkActionService service, CancellationToken cancellationToken)
    {
        try { return Results.Ok(await service.ExecuteAsync(request, cancellationToken)); }
        catch (Exception exception) when (IsRequestError(exception)) { return Problem(exception); }
    }

    private static bool IsRequestError(Exception exception) => exception is ArgumentException or UnauthorizedAccessException or IOException or InvalidOperationException;

    private static IResult Problem(Exception exception)
    {
        var status = exception switch
        {
            ArgumentException => StatusCodes.Status400BadRequest,
            UnauthorizedAccessException => StatusCodes.Status428PreconditionRequired,
            IOException => StatusCodes.Status502BadGateway,
            _ => StatusCodes.Status409Conflict,
        };
        return Results.Problem(detail: exception.Message, statusCode: status);
    }
}
