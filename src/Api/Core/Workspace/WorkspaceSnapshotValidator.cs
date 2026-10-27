namespace DTech.Core.Workspace;

public static class WorkspaceSnapshotValidator
{
    private const int MaxSha256Length = 128;

    public static WorkspaceStateError? Validate(WorkspaceSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return new WorkspaceStateError("The workspace snapshot is missing.");
        }

        return WorkspaceStateValidator.Validate(snapshot.State) ?? ValidateDrafts(snapshot.Drafts, snapshot.State.EditorTabs);
    }

    private static WorkspaceStateError? ValidateDrafts(IReadOnlyList<WorkspaceEditorDraft>? drafts, IReadOnlyList<WorkspaceEditorTab> editorTabs)
    {
        if (drafts is null || drafts.Count > WorkspaceStateLimits.MaxEditorTabs)
        {
            return new WorkspaceStateError($"Up to {WorkspaceStateLimits.MaxEditorTabs} unsaved editor drafts can be saved.");
        }

        var openTabs = new HashSet<WorkspaceEditorTab>(editorTabs);
        var draftTabs = new HashSet<WorkspaceEditorTab>();
        long totalCharacters = 0;
        for (var i = 0; i < drafts.Count; i++)
        {
            var draft = drafts[i];
            if (draft is null || draft.Content is null || !HasValidBaseVersion(draft))
            {
                return new WorkspaceStateError("An unsaved editor draft is invalid.");
            }

            var tab = draft.ToTab();
            if (!WorkspaceValueRules.IsEditorTab(tab) || !openTabs.Contains(tab) || !draftTabs.Add(tab))
            {
                return new WorkspaceStateError("Every unsaved editor draft must belong to exactly one open editor tab.");
            }

            if (draft.Content.Length > WorkspaceStateLimits.MaxDraftCharacters)
            {
                return new WorkspaceStateError($"An unsaved editor draft is longer than {WorkspaceStateLimits.MaxDraftCharacters} characters.");
            }

            totalCharacters += draft.Content.Length;
        }

        if (totalCharacters > WorkspaceStateLimits.MaxTotalDraftCharacters)
        {
            return new WorkspaceStateError($"Unsaved editor drafts are longer than {WorkspaceStateLimits.MaxTotalDraftCharacters} characters in total.");
        }

        return null;
    }

    private static bool HasValidBaseVersion(WorkspaceEditorDraft draft)
    {
        var version = draft.BaseVersion;
        if (version is null)
        {
            return true;
        }

        return version.Size >= 0 && !string.IsNullOrEmpty(version.Sha256) && version.Sha256.Length <= MaxSha256Length;
    }
}
