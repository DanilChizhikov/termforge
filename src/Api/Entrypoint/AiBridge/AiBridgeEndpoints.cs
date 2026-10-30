using DTech.Core.AiBridge.Approvals;
using DTech.Core.AiBridge.Settings;
using DTech.Entrypoint.AiBridge;
using DTech.Entrypoint.Security;

namespace Microsoft.AspNetCore.Builder;

public static class AiBridgeEndpoints
{
    public const string RoutePrefix = "/api/ai-bridge";

    public static IEndpointRouteBuilder MapAiBridgeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapMcp(SecurityPaths.AiBridge);
        var group = endpoints.MapGroup(RoutePrefix);
        group.MapGet("/settings", GetSettings);
        group.MapPut("/settings", UpdateSettings);
        group.MapPost("/token", RegenerateToken);
        group.MapGet("/approvals", ListApprovals);
        group.MapPost("/approvals/{id:guid}", Decide);
        return endpoints;
    }

    private static async Task<IResult> GetSettings(HttpRequest request, AiBridgeSettingsService settingsService, CancellationToken cancellationToken)
    {
        var settings = await settingsService.GetAsync(cancellationToken);
        var mcpUrl = AiBridgeUrls.McpUrlOf(request);
        var response = AiBridgeSettingsResponse.From(settings, mcpUrl);
        return Results.Ok(response);
    }

    private static async Task<IResult> UpdateSettings(AiBridgeSettingsBody body, HttpRequest request, AiBridgeSettingsService settingsService)
    {
        var preferences = body.ToPreferences();
        var updated = await settingsService.UpdateAsync(preferences, request.HttpContext.RequestAborted);
        var mcpUrl = AiBridgeUrls.McpUrlOf(request);
        var response = AiBridgeSettingsResponse.From(updated, mcpUrl);
        return Results.Ok(response);
    }

    private static async Task<IResult> RegenerateToken(HttpRequest request, AiBridgeSettingsService settingsService, CancellationToken cancellationToken)
    {
        var token = await settingsService.RegenerateTokenAsync(cancellationToken);
        var mcpUrl = AiBridgeUrls.McpUrlOf(request);
        var response = new AiBridgeTokenResponse(token, mcpUrl);
        return Results.Ok(response);
    }

    private static IResult ListApprovals(AiApprovalBroker broker)
    {
        var pending = broker.GetPending();
        var responses = AiApprovalResponse.FromAll(pending);
        return Results.Ok(responses);
    }

    private static IResult Decide(Guid id, AiApprovalDecisionBody body, AiApprovalBroker broker)
    {
        var outcome = body.Approve ? AiApprovalOutcome.Approved : AiApprovalOutcome.Denied;
        if (!broker.TryResolve(id, outcome))
        {
            return Results.Problem(detail: "The approval request has already been answered or has expired.", statusCode: StatusCodes.Status404NotFound);
        }

        return Results.NoContent();
    }
}
