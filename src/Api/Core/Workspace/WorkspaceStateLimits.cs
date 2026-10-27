namespace DTech.Core.Workspace;

public static class WorkspaceStateLimits
{
    public const int MaxTerminalTabs = 100;
    public const int MaxEditorTabs = 50;
    public const int MaxRemotePaths = 200;
    public const int MaxPanelLayouts = 20;
    public const int MaxPanelsPerLayout = 20;
    public const int MaxCollapsedPanels = 50;
    public const int MaxPathLength = 4096;
    public const int MaxIdentifierLength = 64;
    public const int MaxConnectionNameLength = 256;
    public const int MaxDraftCharacters = 4 * 1024 * 1024;
    public const int MaxTotalDraftCharacters = 16 * 1024 * 1024;
}
