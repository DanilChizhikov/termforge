using DTech.Core.SavedConnections;

namespace DTech.Core.Docker;

public sealed class DockerTargetResolver(ISavedConnectionRepository repository, ConnectionTargetFactory targetFactory)
{
    public async Task<DockerResult<DockerTarget>> ResolveAsync(Guid connectionId, CancellationToken cancellationToken)
    {
        var all = await repository.ListAsync(cancellationToken);
        var connection = JumpHostGuard.Find(all, connectionId);
        if (connection is null)
        {
            var notFound = new DockerError(DockerErrorCode.ConnectionNotFound, $"Saved connection {connectionId} was not found.");
            return DockerResult<DockerTarget>.Failure(notFound);
        }

        var target = await targetFactory.CreateAsync(connection, all, cancellationToken);
        if (!target.IsSuccess)
        {
            var unavailable = new DockerError(DockerErrorCode.CredentialsUnavailable, target.Error.Message);
            return DockerResult<DockerTarget>.Failure(unavailable);
        }

        var dockerTarget = new DockerTarget(connection.Id, connection.Details.Name, target.Value);
        return DockerResult<DockerTarget>.Success(dockerTarget);
    }
}
