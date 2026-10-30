using DTech.Core.Docker;

namespace DTech.Core.AiBridge.Docker;

public sealed class AiDockerQueryService(AiServerAccessPolicy accessPolicy, DockerOverviewService overviewService, DockerImageService imageService)
{
    public async Task<AiBridgeResult<DockerOverview>> ListContainersAsync(Guid connectionId, CancellationToken cancellationToken)
    {
        var access = await accessPolicy.CheckAsync(connectionId, cancellationToken);
        if (!access.IsSuccess)
        {
            return AiBridgeResult<DockerOverview>.Failure(access.Error);
        }

        var overview = await overviewService.GetAsync(connectionId, cancellationToken);
        if (!overview.IsSuccess)
        {
            var failure = AiBridgeErrorMapper.FromDocker(overview.Error);
            return AiBridgeResult<DockerOverview>.Failure(failure);
        }

        return AiBridgeResult<DockerOverview>.Success(overview.Value);
    }

    public async Task<AiBridgeResult<DockerImageList>> ListImagesAsync(Guid connectionId, CancellationToken cancellationToken)
    {
        var access = await accessPolicy.CheckAsync(connectionId, cancellationToken);
        if (!access.IsSuccess)
        {
            return AiBridgeResult<DockerImageList>.Failure(access.Error);
        }

        var images = await imageService.ListAsync(connectionId, cancellationToken);
        if (!images.IsSuccess)
        {
            var failure = AiBridgeErrorMapper.FromDocker(images.Error);
            return AiBridgeResult<DockerImageList>.Failure(failure);
        }

        return AiBridgeResult<DockerImageList>.Success(images.Value);
    }
}
