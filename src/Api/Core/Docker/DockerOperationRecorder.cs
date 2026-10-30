using DTech.Core.Operations;

namespace DTech.Core.Docker;

public sealed class DockerOperationRecorder(IOperationJournal journal)
{
    public const string StartKind = "docker.start";
    public const string StopKind = "docker.stop";
    public const string RestartKind = "docker.restart";
    public const string RemoveKind = "docker.remove";
    public const string ComposeRestartKind = "docker.compose.restart";
    public const string ComposeRecreateKind = "docker.compose.recreate";
    public const string ImageRemoveKind = "docker.image.remove";
    public const string PullKind = "docker.pull";

    public static string KindOf(DockerContainerAction action)
    {
        return action switch
        {
            DockerContainerAction.Start => StartKind,
            DockerContainerAction.Stop => StopKind,
            DockerContainerAction.Restart => RestartKind,
            DockerContainerAction.Remove => RemoveKind,
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unsupported container action."),
        };
    }

    public Task<Guid> StartAsync(DockerOperationStart start, CancellationToken cancellationToken)
    {
        var details = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["container"] = start.Container.Value,
            ["action"] = start.Action.ToString().ToLowerInvariant(),
        };
        var kind = KindOf(start.Action);
        var entry = new DockerOperationEntry(kind, start.Server, details);
        return StartAsync(entry, cancellationToken);
    }

    public async Task<Guid> StartAsync(DockerOperationEntry entry, CancellationToken cancellationToken)
    {
        var request = new CreateOperationRequest(entry.Kind, entry.Server, entry.Details, false);
        var created = await journal.Create(request, cancellationToken);
        await journal.Start(created.Id, cancellationToken);
        return created.Id;
    }

    public Task SucceedAsync(Guid operationId)
    {
        return journal.Succeed(operationId, null, CancellationToken.None);
    }

    public Task FailAsync(Guid operationId, DockerError error)
    {
        return journal.Fail(operationId, error.Message, CancellationToken.None);
    }
}
