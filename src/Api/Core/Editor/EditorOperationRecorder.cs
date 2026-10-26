using System.Globalization;
using DTech.Core.Operations;

namespace DTech.Core.Editor;

public sealed class EditorOperationRecorder(IOperationJournal journal)
{
    public const string SaveKind = "file.save";
    public const string RollbackKind = "file.rollback";

    public async Task<Guid> StartAsync(EditorOperationStart start, CancellationToken cancellationToken)
    {
        var details = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["path"] = start.Path,
        };
        if (start.BackupName is not null)
        {
            details["restoredFrom"] = start.BackupName;
        }

        var request = new CreateOperationRequest(start.Kind, start.Server, details, false);
        var created = await journal.Create(request, cancellationToken);
        await journal.Start(created.Id, cancellationToken);
        return created.Id;
    }

    public async Task CompleteAsync(Guid operationId, EditorResult<RemoteSaveResult> result)
    {
        if (!result.IsSuccess)
        {
            await journal.Fail(operationId, result.Error.Message, CancellationToken.None);
            return;
        }

        if (!result.Value.IsSaved)
        {
            await journal.Fail(operationId, $"Not saved: {result.Value.Conflict.Reason}", CancellationToken.None);
            return;
        }

        var saved = result.Value.Saved;
        var details = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["strategy"] = saved.Strategy.ToString(),
            ["bytes"] = saved.Version.Size.ToString(CultureInfo.InvariantCulture),
            ["sha256"] = saved.Version.Sha256,
        };
        if (saved.Backup is not null)
        {
            details["backup"] = saved.Backup.Name;
        }

        if (saved.Warnings.Count > 0)
        {
            details["warnings"] = string.Join(" ", saved.Warnings);
        }

        await journal.Succeed(operationId, details, CancellationToken.None);
    }
}
