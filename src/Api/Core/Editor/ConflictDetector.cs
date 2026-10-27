namespace DTech.Core.Editor;

public sealed class ConflictDetector(TextContentCodec codec)
{
    public const string ChangedReason = "The file changed on the server after you opened it.";
    public const string DeletedReason = "The file was deleted on the server after you opened it.";
    public const string CreatedReason = "A file with this name now exists on the server.";

    public EditorConflict? Detect(ConflictCheck check)
    {
        if (check.Current is null)
        {
            return check.ExpectedVersion is null ? null : new EditorConflict(null, null, DeletedReason);
        }

        if (check.ExpectedVersion is null)
        {
            return Describe(check, CreatedReason);
        }

        return check.ExpectedVersion == check.Current.Version ? null : Describe(check, ChangedReason);
    }

    private EditorConflict Describe(ConflictCheck check, string reason)
    {
        var current = check.Current!;
        var decoded = codec.Decode(current.Content, check.Path);
        TextContent? content = decoded.IsSuccess ? decoded.Value : null;
        return new EditorConflict(current.Version, content, reason);
    }
}
