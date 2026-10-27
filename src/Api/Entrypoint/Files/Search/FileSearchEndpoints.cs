using DTech.Core.Files.Search;
using DTech.Entrypoint.Files;
using DTech.Entrypoint.Files.Search;

namespace Microsoft.AspNetCore.Builder;

public static class FileSearchEndpoints
{
    public const string LocalSearchPath = "/api/files/local/search";
    public const string RemoteSearchPath = "/api/files/remote/{connectionId:guid}/search";

    public static IEndpointRouteBuilder MapFileSearchEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(LocalSearchPath, SearchLocal);
        endpoints.MapGet(RemoteSearchPath, SearchRemote);
        return endpoints;
    }

    private static IResult SearchLocal([AsParameters] FileSearchParameters parameters, LocalFileSearchService service)
    {
        var request = parameters.ToRequest();
        var started = service.Start(request);
        if (!started.IsSuccess)
        {
            return FileErrorResults.ToProblem(started.Error);
        }

        return new FileSearchStreamResult(started.Value);
    }

    private static async Task<IResult> SearchRemote(Guid connectionId, [AsParameters] FileSearchParameters parameters, RemoteFileSearchService service, CancellationToken cancellationToken)
    {
        var search = parameters.ToRequest();
        var request = new RemoteFileSearchRequest(search, parameters.UsesRemoteFind);
        var started = await service.StartAsync(connectionId, request, cancellationToken);
        if (!started.IsSuccess)
        {
            return FileErrorResults.ToProblem(started.Error);
        }

        return new FileSearchStreamResult(started.Value);
    }
}
