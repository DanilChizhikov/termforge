using DTech.Core.Operations;

namespace DTech.Core.AiBridge;

public sealed class AiOperationRecorder(IOperationJournal journal)
{
    public const string CommandKind = "ai.command";
    public const string TerminalInputKind = "ai.terminal_input";

    public async Task<Guid> StartAsync(AiOperationEntry entry, CancellationToken cancellationToken)
    {
        var request = new CreateOperationRequest(entry.Kind, entry.Server, entry.Details, false);
        var created = await journal.Create(request, cancellationToken);
        await journal.Start(created.Id, cancellationToken);
        return created.Id;
    }

    public Task SucceedAsync(Guid operationId, IReadOnlyDictionary<string, string>? details)
    {
        return journal.Succeed(operationId, details, CancellationToken.None);
    }

    public Task FailAsync(Guid operationId, string error)
    {
        return journal.Fail(operationId, error, CancellationToken.None);
    }
}
