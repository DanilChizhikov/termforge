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
        group.MapPost("/compose/{project}/{action}", RunComposeAction);
        group.MapGet("/images", ListImages);
        group.MapPost("/images/remove", RemoveImage);
        group.Map("/images/pull/ws", PullImage);
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
        if (!TryParseName<DockerContainerAction>(call.Action, out var parsedAction))
        {
            var unknownAction = new DockerError(DockerErrorCode.InvalidRequest, "Unsupported container action. Use start, stop, restart or remove.");
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

    private static async Task<IResult> RunComposeAction([AsParameters] ComposeActionCall call, ComposeActionService service, CancellationToken cancellationToken)
    {
        if (!TryParseName<ComposeAction>(call.Action, out var parsedAction))
        {
            var unknownAction = new DockerError(DockerErrorCode.InvalidRequest, "Unsupported compose action. Use restart or recreate.");
            return DockerErrorResults.ToProblem(unknownAction);
        }

        var request = new ComposeActionRequest(call.Id, call.Project, parsedAction) { ConfirmationToken = call.Body?.ConfirmationToken };
        var result = await service.RunAsync(request, cancellationToken);
        if (!result.IsSuccess)
        {
            return DockerErrorResults.ToProblem(result.Error);
        }

        var completed = result.Value;
        var actionName = completed.Action.ToString().ToLowerInvariant();
        var response = new ComposeActionResponse(completed.OperationId, completed.Project, actionName);
        return Results.Ok(response);
    }

    private static async Task<IResult> ListImages(Guid id, DockerImageService service, CancellationToken cancellationToken)
    {
        var result = await service.ListAsync(id, cancellationToken);
        if (!result.IsSuccess)
        {
            return DockerErrorResults.ToProblem(result.Error);
        }

        var response = DockerImagesResponse.From(result.Value);
        return Results.Ok(response);
    }

    private static async Task<IResult> RemoveImage([AsParameters] ImageRemoveCall call, ImageRemoveService service, CancellationToken cancellationToken)
    {
        var request = new ImageRemoveRequest(call.Id, call.Body.Image) { ConfirmationToken = call.Body.ConfirmationToken };
        var result = await service.RemoveAsync(request, cancellationToken);
        if (!result.IsSuccess)
        {
            return DockerErrorResults.ToProblem(result.Error);
        }

        var response = new ImageRemoveResponse(result.Value.OperationId, result.Value.Image);
        return Results.Ok(response);
    }

    private static async Task<IResult> PullImage([AsParameters] ImagePullCall call, ImagePullService service, DockerOutputRelay relay)
    {
        var context = call.Context;
        if (!context.WebSockets.IsWebSocketRequest)
        {
            var notWebSocket = new DockerError(DockerErrorCode.InvalidRequest, "This endpoint accepts WebSocket connections only.");
            return DockerErrorResults.ToProblem(notWebSocket);
        }

        var request = new ImagePullRequest(call.Id, call.Image);
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

    private static async Task<IResult> StreamLogs([AsParameters] ContainerLogCall call, ContainerLogService service, DockerOutputRelay relay)
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

    private static bool TryParseName<TEnum>(string? value, out TEnum parsed)
        where TEnum : struct, Enum
    {
        var isNumeric = int.TryParse(value, out _);
        var isKnown = Enum.TryParse(value, true, out parsed) && Enum.IsDefined(parsed);
        return isKnown && !isNumeric;
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
