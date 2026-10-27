using DTech.Core.Editor;
using DTech.Core.SavedConnections;
using DTech.Core.Workspace;

namespace DTech.Core.Tests.Workspace;

internal static class WorkspaceSamples
{
    public static readonly Guid WebId = Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e");
    public static readonly Guid DatabaseId = Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7");

    public static WorkspaceState State()
    {
        var sizes = new Dictionary<string, double> { ["servers"] = 25, ["main"] = 75 };
        var editorTab = new WorkspaceEditorTab(WebId, "/etc/nginx/nginx.conf");
        return new WorkspaceState
        {
            SelectedConnectionId = WebId,
            ActiveView = "editor",
            TerminalTabs = [new WorkspaceTerminalTab("terminal-1", WebId, "web"), new WorkspaceTerminalTab("terminal-2", DatabaseId, "db")],
            ActiveTerminalKey = "terminal-2",
            LocalPath = "/Users/dev/projects",
            RemotePaths = [new WorkspaceRemotePath(WebId, "/var/www"), new WorkspaceRemotePath(DatabaseId, "/srv")],
            PanelLayouts = [new WorkspacePanelLayout("app-layout", sizes)],
            CollapsedPanels = ["remote-files"],
            EditorTabs = [editorTab, new WorkspaceEditorTab(DatabaseId, "/etc/hosts")],
            ActiveEditorTab = editorTab,
        };
    }

    public static WorkspaceEditorDraft Draft(Guid connectionId, string path)
    {
        var modifiedUtc = new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc);
        var baseVersion = new RemoteFileVersion(modifiedUtc, 120, "abc123");
        return new WorkspaceEditorDraft(connectionId, path, "worker_processes 4;", baseVersion);
    }

    public static SavedConnection Connection(Guid id, string name)
    {
        var details = new SavedConnectionDraft { Name = name, Host = $"{name}.example.org", UserName = "deploy" };
        return new SavedConnection { Id = id, Details = details };
    }
}
