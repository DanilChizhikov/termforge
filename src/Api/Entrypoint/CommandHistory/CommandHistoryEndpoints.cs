using DTech.Core.CommandHistory;
using DTech.Entrypoint.CommandHistory;

namespace Microsoft.AspNetCore.Builder;

public static class CommandHistoryEndpoints
{
    public const string ConnectionRoutePrefix = "/api/connections";
    public const string RoutePrefix = "/api/command-history";

    public static IEndpointRouteBuilder MapCommandHistoryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var connections = endpoints.MapGroup(ConnectionRoutePrefix);
        connections.MapGet("/{connectionId:guid}/history", List);
        connections.MapDelete("/{connectionId:guid}/history", Clear);

        var history = endpoints.MapGroup(RoutePrefix);
        history.MapPut("/{id:guid}/pin", Pin);
        history.MapDelete("/{id:guid}", Delete);
        history.MapGet("/exclusions", GetExclusions);
        history.MapPut("/exclusions", SaveExclusions);
        history.MapDelete("/exclusions", ResetExclusions);
        history.MapGet("/integration/{shell}", GetIntegration);
        return endpoints;
    }

    private static async Task<IResult> List(Guid connectionId, string? search, int? limit, CommandHistoryService service, CancellationToken cancellationToken)
    {
        var pageSize = limit ?? CommandHistoryLimits.DefaultPageSize;
        var query = new CommandHistoryQuery(connectionId, search, pageSize);
        var entries = await service.ListAsync(query, cancellationToken);
        var responses = new List<CommandHistoryResponse>(entries.Count);
        for (var i = 0; i < entries.Count; i++)
        {
            responses.Add(CommandHistoryResponse.From(entries[i]));
        }

        return Results.Ok(responses);
    }

    private static async Task<IResult> Clear(Guid connectionId, CommandHistoryService service, CancellationToken cancellationToken)
    {
        await service.ClearUnpinnedAsync(connectionId, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> Pin(Guid id, PinCommandRequest request, CommandHistoryService service, CancellationToken cancellationToken)
    {
        var updated = await service.SetPinnedAsync(id, request.Pinned, cancellationToken);
        return updated ? Results.NoContent() : CreateNotFound();
    }

    private static async Task<IResult> Delete(Guid id, CommandHistoryService service, CancellationToken cancellationToken)
    {
        var deleted = await service.DeleteAsync(id, cancellationToken);
        return deleted ? Results.NoContent() : CreateNotFound();
    }

    private static async Task<IResult> GetExclusions(CommandExclusionSettings settings, CancellationToken cancellationToken)
    {
        var patterns = await settings.GetPatternsAsync(cancellationToken);
        var response = new ExclusionPatternsResponse(patterns, DefaultCommandExclusionPatterns.Values);
        return Results.Ok(response);
    }

    private static async Task<IResult> SaveExclusions(ExclusionPatternsRequest request, CommandExclusionSettings settings, CancellationToken cancellationToken)
    {
        if (request.Patterns is null)
        {
            return Results.Problem(detail: "The list of patterns is required.", statusCode: StatusCodes.Status400BadRequest);
        }

        var error = await settings.SavePatternsAsync(request.Patterns, cancellationToken);
        if (error is not null)
        {
            return Results.Problem(detail: error, statusCode: StatusCodes.Status400BadRequest);
        }

        return await GetExclusions(settings, cancellationToken);
    }

    private static async Task<IResult> ResetExclusions(CommandExclusionSettings settings, CancellationToken cancellationToken)
    {
        await settings.ResetAsync(cancellationToken);
        return await GetExclusions(settings, cancellationToken);
    }

    private static IResult GetIntegration(string shell)
    {
        if (!Enum.TryParse<ShellKind>(shell, true, out var kind) || !Enum.IsDefined(kind))
        {
            return Results.Problem(detail: $"The shell '{shell}' is not supported. Use bash or zsh.", statusCode: StatusCodes.Status400BadRequest);
        }

        var snippet = ShellIntegrationSnippets.For(kind);
        var response = new ShellIntegrationResponse(kind.ToString().ToLowerInvariant(), snippet);
        return Results.Ok(response);
    }

    private static IResult CreateNotFound()
    {
        return Results.Problem(detail: "The history entry does not exist.", statusCode: StatusCodes.Status404NotFound);
    }
}
