using DTech.Core.SavedConnections;
using DTech.Entrypoint.SavedConnections;

namespace Microsoft.AspNetCore.Builder;

public static class SavedConnectionEndpoints
{
    public const string RoutePrefix = "/api/connections";
    public const string GroupRoutePrefix = "/api/connection-groups";

    public static IEndpointRouteBuilder MapSavedConnectionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(RoutePrefix);
        group.MapGet("/", List);
        group.MapPost("/", Create);
        group.MapGet("/{id:guid}", Get);
        group.MapGet("/{id:guid}/password", RevealPassword);
        group.MapPut("/{id:guid}", Update);
        group.MapDelete("/{id:guid}", Delete);
        group.MapPost("/{id:guid}/test", Test);
        group.MapPost("/import/ssh-config", ImportSshConfig);

        var groups = endpoints.MapGroup(GroupRoutePrefix);
        groups.MapPut("/{name}", RenameGroup);
        groups.MapDelete("/{name}", RemoveGroup);
        return endpoints;
    }

    private static async Task<IResult> List(SavedConnectionService service, CancellationToken cancellationToken)
    {
        var connections = await service.ListAsync(cancellationToken);
        var responses = new List<SavedConnectionResponse>(connections.Count);
        for (var i = 0; i < connections.Count; i++)
        {
            responses.Add(SavedConnectionResponse.From(connections[i]));
        }

        return Results.Ok(responses);
    }

    private static async Task<IResult> Get(Guid id, SavedConnectionService service, CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(id, cancellationToken);
        return ToOk(result);
    }

    private static async Task<IResult> RevealPassword(Guid id, SavedConnectionService service, CancellationToken cancellationToken)
    {
        var result = await service.RevealPasswordAsync(id, cancellationToken);
        if (!result.IsSuccess)
        {
            return SavedConnectionErrorResults.ToProblem(result.Error);
        }

        var response = new SavedConnectionPasswordResponse { Password = result.Value };
        return Results.Ok(response);
    }

    private static async Task<IResult> Create(SavedConnectionRequest request, SavedConnectionService service, CancellationToken cancellationToken)
    {
        var submission = SavedConnectionRequestMapper.ToSubmission(request);
        if (!submission.IsSuccess)
        {
            return SavedConnectionErrorResults.ToProblem(submission.Error);
        }

        var result = await service.CreateAsync(submission.Value, cancellationToken);
        if (!result.IsSuccess)
        {
            return SavedConnectionErrorResults.ToProblem(result.Error);
        }

        var response = SavedConnectionResponse.From(result.Value);
        return Results.Created($"{RoutePrefix}/{response.Id}", response);
    }

    private static async Task<IResult> Update(Guid id, SavedConnectionRequest request, SavedConnectionService service, CancellationToken cancellationToken)
    {
        var submission = SavedConnectionRequestMapper.ToSubmission(request);
        if (!submission.IsSuccess)
        {
            return SavedConnectionErrorResults.ToProblem(submission.Error);
        }

        var result = await service.UpdateAsync(id, submission.Value, cancellationToken);
        return ToOk(result);
    }

    private static async Task<IResult> Delete(Guid id, SavedConnectionService service, CancellationToken cancellationToken)
    {
        var result = await service.DeleteAsync(id, cancellationToken);
        return result.IsSuccess ? Results.NoContent() : SavedConnectionErrorResults.ToProblem(result.Error);
    }

    private static async Task<IResult> Test(Guid id, ConnectionTester tester, CancellationToken cancellationToken)
    {
        var result = await tester.TestAsync(id, cancellationToken);
        if (!result.IsSuccess)
        {
            return SavedConnectionErrorResults.ToProblem(result.Error);
        }

        var response = ConnectionTestResponse.From(result.Value);
        return Results.Ok(response);
    }

    private static async Task<IResult> ImportSshConfig(SshConfigImporter importer, CancellationToken cancellationToken)
    {
        var result = await importer.ImportAsync(cancellationToken);
        if (!result.IsSuccess)
        {
            return SavedConnectionErrorResults.ToProblem(result.Error);
        }

        var response = SshConfigImportResponse.From(result.Value);
        return Results.Ok(response);
    }

    private static async Task<IResult> RenameGroup(string name, RenameGroupRequest request, SavedConnectionService service, CancellationToken cancellationToken)
    {
        var result = await service.RenameGroupAsync(name, request.NewName ?? string.Empty, cancellationToken);
        return result.IsSuccess ? Results.NoContent() : SavedConnectionErrorResults.ToProblem(result.Error);
    }

    private static async Task<IResult> RemoveGroup(string name, SavedConnectionService service, CancellationToken cancellationToken)
    {
        var result = await service.RemoveGroupAsync(name, cancellationToken);
        return result.IsSuccess ? Results.NoContent() : SavedConnectionErrorResults.ToProblem(result.Error);
    }

    private static IResult ToOk(SavedConnectionResult<SavedConnection> result)
    {
        if (!result.IsSuccess)
        {
            return SavedConnectionErrorResults.ToProblem(result.Error);
        }

        var response = SavedConnectionResponse.From(result.Value);
        return Results.Ok(response);
    }
}
