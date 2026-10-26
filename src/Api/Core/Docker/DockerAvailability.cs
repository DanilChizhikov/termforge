namespace DTech.Core.Docker;

public sealed record DockerAvailability(DockerAvailabilityState State, string Reason)
{
    public bool IsAvailable => State == DockerAvailabilityState.Available;
}
