namespace DTech.Core.Docker;

public sealed record ImagePullRequest(Guid ConnectionId, string? Image);
