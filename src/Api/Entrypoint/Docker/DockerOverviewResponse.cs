using DTech.Core.Docker;

namespace DTech.Entrypoint.Docker;

internal sealed record DockerOverviewResponse(Guid ConnectionId, string ServerName, DockerAvailabilityResponse Availability, IReadOnlyList<DockerContainerResponse> Containers)
{
    public static DockerOverviewResponse From(DockerOverview overview)
    {
        var availabilityState = overview.Availability.State.ToString();
        var availability = new DockerAvailabilityResponse(availabilityState, overview.Availability.Reason);
        var containers = new List<DockerContainerResponse>(overview.Containers.Count);
        for (var i = 0; i < overview.Containers.Count; i++)
        {
            var container = DockerContainerResponse.From(overview.Containers[i]);
            containers.Add(container);
        }

        return new DockerOverviewResponse(overview.ConnectionId, overview.ServerName, availability, containers);
    }
}
