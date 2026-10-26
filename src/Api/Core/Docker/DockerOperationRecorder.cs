using DTech.Core.Operations;

namespace DTech.Core.Docker;

public sealed class DockerOperationRecorder(IOperationJournal journal)
{
    public const string StartKind = "docker.start";
    public const string StopKind = "docker.stop";
    public const string RestartKind = "docker.restart";

    public static string KindOf(DockerContainerAction action)
    {
        return action switch
        {
            DockerContainerAction.Start => StartKind,
            DockerContainerAction.Stop => StopKind,
            DockerContainerAction.Restart => RestartKind,
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unsupported container action."),
        };
    }

    public async Task<Guid> StartAsync(DockerOperationStart start, CancellationToken cancellationToken)
    {
        var details = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["container"] = start.Container.Value,
            ["action"] = start.Action.ToString().ToLowerInvariant(),
        };
        var kind = KindOf(start.Action);
        var request = new CreateOperationRequest(kind, start.Server, details, false);
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
