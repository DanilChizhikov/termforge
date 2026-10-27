namespace Microsoft.AspNetCore.Builder;

public static class FileBrowsingEndpoints
{
    public static IEndpointRouteBuilder MapFileBrowsingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapLocalFileEndpoints();
        endpoints.MapRemoteFileEndpoints();
        return endpoints;
    }
}
