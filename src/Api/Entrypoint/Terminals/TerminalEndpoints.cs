using DTech.Core.Terminal;
using DTech.Core.Terminal.Sessions;
using DTech.Entrypoint.Terminals;

namespace Microsoft.AspNetCore.Builder;

public static class TerminalEndpoints
{
    public const string RoutePrefix = "/api/terminals";

    public static IEndpointRouteBuilder MapTerminalEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(RoutePrefix);
        group.MapGet("/", List);
        group.MapPost("/", Open);
        group.MapDelete("/{id}", Close);
        group.Map("/{id}/ws", Attach);
        return endpoints;
    }

    private static IResult List(HttpContext context, TerminalSessionManager sessionManager)
    {
        var owner = TerminalOwner.From(context);
        var descriptors = sessionManager.List(owner);
        var responses = new List<TerminalSessionResponse>(descriptors.Count);
        for (var i = 0; i < descriptors.Count; i++)
        {
            var response = TerminalSessionResponse.From(descriptors[i]);
            responses.Add(response);
        }

        return Results.Ok(responses);
    }

    private static async Task<IResult> Open(OpenTerminalRequest request, HttpContext context, TerminalLauncher launcher)
    {
        if (request.ConnectionId is null)
        {
            var missingConnection = new TerminalError(TerminalErrorCode.InvalidRequest, "Select a saved connection to open a terminal.");
            return TerminalErrorResults.ToProblem(missingConnection);
        }

        var owner = TerminalOwner.From(context);
        var size = new TerminalSize(request.Columns, request.Rows);
        var launchRequest = new TerminalLaunchRequest(request.ConnectionId.Value, owner, size);
        var launched = await launcher.LaunchAsync(launchRequest, context.RequestAborted);
        if (!launched.IsSuccess)
        {
            return TerminalErrorResults.ToProblem(launched.Error);
        }

        var response = TerminalSessionResponse.From(launched.Value.Descriptor);
        return Results.Created($"{RoutePrefix}/{response.Id}", response);
    }

    private static async Task<IResult> Close(string id, HttpContext context, TerminalSessionManager sessionManager)
    {
        var owner = TerminalOwner.From(context);
        var sessionId = new TerminalSessionId(id);
        var closed = await sessionManager.CloseAsync(sessionId, owner, TerminalCloseReasons.ClosedByUser);
        return closed ? Results.NoContent() : CreateNotFound();
    }

    private static async Task<IResult> Attach(string id, HttpContext context, TerminalSessionManager sessionManager, TerminalWebSocketBridge bridge)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            var notWebSocket = new TerminalError(TerminalErrorCode.InvalidRequest, "This endpoint accepts WebSocket connections only.");
            return TerminalErrorResults.ToProblem(notWebSocket);
        }

        var owner = TerminalOwner.From(context);
        var sessionId = new TerminalSessionId(id);
        var session = sessionManager.Find(sessionId, owner);
        if (session is null)
        {
            return CreateNotFound();
        }

        if (!session.TryAttach())
        {
            return Results.Problem(detail: "The terminal is already attached to another connection.", statusCode: StatusCodes.Status409Conflict);
        }

        using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        await bridge.RunAsync(webSocket, session, context.RequestAborted);
        return Results.Empty;
    }

    private static IResult CreateNotFound()
    {
        var notFound = new TerminalError(TerminalErrorCode.NotFound, "The terminal session does not exist or belongs to another browser session.");
        return TerminalErrorResults.ToProblem(notFound);
    }
}
