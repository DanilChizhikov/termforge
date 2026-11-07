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
        group.MapGet("/tokens", ListTokens);
        group.MapPost("/tokens", CreateToken);
        group.MapDelete("/tokens/{id:guid}", RevokeToken);
        group.MapGet("/approvals", ListApprovals);
        group.MapPost("/approvals/{id:guid}", Decide);
        return endpoints;
    }

    private static async Task<IResult> GetSettings(HttpRequest request, AiBridgeSettingsService settingsService, AiBridgeTokenService tokens)
    {
        var cancellationToken = request.HttpContext.RequestAborted;
        var settings = await settingsService.GetAsync(cancellationToken);
        var hasToken = await tokens.HasAnyAsync(cancellationToken);
        var mcpUrl = AiBridgeUrls.McpUrlOf(request);
        var response = AiBridgeSettingsResponse.From(settings, mcpUrl, hasToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> UpdateSettings(AiBridgeSettingsBody body, HttpRequest request, [AsParameters] AiBridgeSettingsCall services)
    {
        var preferences = body.ToPreferences();
        var cancellationToken = request.HttpContext.RequestAborted;
        var updated = await services.Settings.UpdateAsync(preferences, cancellationToken);
        var hasToken = await services.Tokens.HasAnyAsync(cancellationToken);
        var mcpUrl = AiBridgeUrls.McpUrlOf(request);
        var response = AiBridgeSettingsResponse.From(updated, mcpUrl, hasToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> CreateToken(AiBridgeTokenBody body, HttpRequest request, AiBridgeTokenService tokens)
    {
        var created = await tokens.CreateAsync(body.Name, request.HttpContext.RequestAborted);
        if (created.Error is { } error)
        {
            var status = error == AiBridgeTokenCreationError.InvalidName
                ? StatusCodes.Status400BadRequest : StatusCodes.Status409Conflict;
            var detail = error == AiBridgeTokenCreationError.InvalidName
                ? "Token name must contain 1 to 100 characters." : "A token with this name already exists.";
            return Results.Problem(detail: detail, statusCode: status);
        }

        var mcpUrl = AiBridgeUrls.McpUrlOf(request);
        var info = created.Info!;
        var response = new AiBridgeTokenResponse
        {
            Id = info.Id,
            Name = info.Name,
            CreatedAt = info.CreatedAt,
            Token = created.Token!,
            McpUrl = mcpUrl,
        };
        request.HttpContext.Response.Headers.CacheControl = "no-store";
        var location = $"{RoutePrefix}/tokens/{info.Id}";
        return Results.Created(location, response);
    }

    private static async Task<IResult> ListTokens(AiBridgeTokenService tokens, CancellationToken cancellationToken)
    {
        var list = await tokens.ListAsync(cancellationToken);
        return Results.Ok(list);
    }

    private static async Task<IResult> RevokeToken(Guid id, AiBridgeTokenService tokens, CancellationToken cancellationToken)
    {
        var deleted = await tokens.RevokeAsync(id, cancellationToken);
        return deleted ? Results.NoContent() : Results.NotFound();
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
