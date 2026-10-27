using DTech.Core.Docker;
using DTech.Core.Terminal;
using DTech.Core.Terminal.Sessions;
using DTech.Entrypoint.Docker;
using DTech.Entrypoint.Terminals;

namespace Microsoft.AspNetCore.Builder;

public static class DockerEndpoints
{
    public const string RoutePrefix = "/api/connections/{id:guid}/docker";

    public static IEndpointRouteBuilder MapDockerEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(RoutePrefix);
        group.MapGet("/", GetOverview);
        group.MapPost("/containers/{container}/shell", OpenShell);
        group.MapPost("/containers/{container}/{action}", RunAction);
        group.Map("/containers/{container}/logs/ws", StreamLogs);
        return endpoints;
    }

    private static async Task<IResult> GetOverview(Guid id, DockerOverviewService service, CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(id, cancellationToken);
        if (!result.IsSuccess)
        {
            return DockerErrorResults.ToProblem(result.Error);
        }

        var response = DockerOverviewResponse.From(result.Value);
        return Results.Ok(response);
    }

    private static async Task<IResult> RunAction([AsParameters] ContainerActionCall call, ContainerActionService service, CancellationToken cancellationToken)
    {
        var isNumeric = int.TryParse(call.Action, out _);
        var isKnown = Enum.TryParse<DockerContainerAction>(call.Action, true, out var parsedAction) && Enum.IsDefined(parsedAction);
        if (!isKnown || isNumeric)
        {
            var unknownAction = new DockerError(DockerErrorCode.InvalidRequest, "Unsupported container action. Use start, stop or restart.");
            return DockerErrorResults.ToProblem(unknownAction);
        }

        var request = new ContainerActionRequest(call.Id, call.Container, parsedAction) { ConfirmationToken = call.Body?.ConfirmationToken };
        var result = await service.RunAsync(request, cancellationToken);
        if (!result.IsSuccess)
        {
            return DockerErrorResults.ToProblem(result.Error);
        }

        var completed = result.Value;
        var actionName = completed.Action.ToString().ToLowerInvariant();
        var response = new ContainerActionResponse(completed.OperationId, completed.Container, actionName);
        return Results.Ok(response);
    }

    private static async Task<IResult> OpenShell([AsParameters] ContainerShellCall call, ContainerShellLauncher launcher)
    {
        if (!TryParseShell(call.Body.Shell, out var shell))
        {
            var unknownShell = new TerminalError(TerminalErrorCode.InvalidRequest, "Unsupported shell. Use auto, bash or sh.");
            return TerminalErrorResults.ToProblem(unknownShell);
        }

        var owner = TerminalOwner.From(call.Context);
        var size = new TerminalSize(call.Body.Columns, call.Body.Rows);
        var launch = new TerminalLaunchRequest(call.Id, owner, size);
        var request = new ContainerShellRequest(launch, call.Container, shell);
        var launched = await launcher.LaunchAsync(request, call.Context.RequestAborted);
        if (!launched.IsSuccess)
        {
            return TerminalErrorResults.ToProblem(launched.Error);
        }

        var response = TerminalSessionResponse.From(launched.Value.Descriptor);
        return Results.Created($"{TerminalEndpoints.RoutePrefix}/{response.Id}", response);
    }

    private static async Task<IResult> StreamLogs([AsParameters] ContainerLogCall call, ContainerLogService service, ContainerLogRelay relay)
    {
        var context = call.Context;
        if (!context.WebSockets.IsWebSocketRequest)
        {
            var notWebSocket = new DockerError(DockerErrorCode.InvalidRequest, "This endpoint accepts WebSocket connections only.");
            return DockerErrorResults.ToProblem(notWebSocket);
        }

        var options = ContainerLogOptions.Create(call.Tail, call.Follow ?? false);
        var request = new ContainerLogRequest(call.Id, call.Container, options);
        using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        var opened = await service.OpenAsync(request, context.RequestAborted);
        if (!opened.IsSuccess)
        {
            await relay.RejectAsync(webSocket, opened.Error, context.RequestAborted);
            return Results.Empty;
        }

        await relay.RunAsync(webSocket, opened.Value, context.RequestAborted);
        return Results.Empty;
    }

    private static bool TryParseShell(string? value, out ContainerShell shell)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            shell = ContainerShell.Auto;
            return true;
        }

        var isNumeric = int.TryParse(value, out _);
        var isKnown = Enum.TryParse(value, true, out shell) && Enum.IsDefined(shell);
        return isKnown && !isNumeric;
    }
}
