using DTech.Core.Diagnostics;

namespace DTech.Core.Docker;

public sealed class DockerOverviewService
{
    private readonly DockerTargetResolver _targetResolver;
    private readonly IRemoteCommandRunnerFactory _runnerFactory;
    private readonly DockerAvailabilityChecker _availabilityChecker;
    private readonly DockerContainerLister _containerLister;

    public DockerOverviewService(
        DockerTargetResolver targetResolver,
        IRemoteCommandRunnerFactory runnerFactory,
        DockerAvailabilityChecker availabilityChecker,
        DockerContainerLister containerLister)
    {
        _targetResolver = targetResolver;
        _runnerFactory = runnerFactory;
        _availabilityChecker = availabilityChecker;
        _containerLister = containerLister;
    }

    public async Task<DockerResult<DockerOverview>> GetAsync(Guid connectionId, CancellationToken cancellationToken)
    {
        var resolved = await _targetResolver.ResolveAsync(connectionId, cancellationToken);
        if (!resolved.IsSuccess)
        {
            return DockerResult<DockerOverview>.Failure(resolved.Error);
        }

        var target = resolved.Value;
        var connected = await _runnerFactory.ConnectAsync(target.Target, cancellationToken);
        if (!connected.IsSuccess)
        {
            var connectionError = DockerError.FromSsh(connected.Error!);
            return DockerResult<DockerOverview>.Failure(connectionError);
        }

        await using var runner = connected.Value;
        return await CollectAsync(target, runner, cancellationToken);
    }

    private async Task<DockerResult<DockerOverview>> CollectAsync(DockerTarget target, IRemoteCommandRunner runner, CancellationToken cancellationToken)
    {
        var context = new CapabilityProbeContext(target.Target, runner);
        var availability = await _availabilityChecker.CheckAsync(context, cancellationToken);
        if (!availability.IsAvailable)
        {
            var unavailable = new DockerOverview(target.ConnectionId, target.ServerName, availability, []);
            return DockerResult<DockerOverview>.Success(unavailable);
        }

        var listed = await _containerLister.ListAsync(runner, cancellationToken);
        if (!listed.IsSuccess)
        {
            return DockerResult<DockerOverview>.Failure(listed.Error);
        }

        var overview = new DockerOverview(target.ConnectionId, target.ServerName, availability, listed.Value);
        return DockerResult<DockerOverview>.Success(overview);
    }
}
