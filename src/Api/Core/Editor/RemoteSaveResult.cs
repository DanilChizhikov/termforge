using System.Diagnostics.CodeAnalysis;

namespace DTech.Core.Editor;

public sealed record RemoteSaveResult
{
    private RemoteSaveResult(SavedFile? saved, EditorConflict? conflict)
    {
        Saved = saved;
        Conflict = conflict;
    }

    public SavedFile? Saved { get; }

    public EditorConflict? Conflict { get; }

    [MemberNotNullWhen(true, nameof(Saved))]
    [MemberNotNullWhen(false, nameof(Conflict))]
    public bool IsSaved => Saved is not null;

    public static RemoteSaveResult Completed(SavedFile saved)
    {
        return new RemoteSaveResult(saved, null);
    }

    public static RemoteSaveResult Conflicted(EditorConflict conflict)
    {
        return new RemoteSaveResult(null, conflict);
    }
}
