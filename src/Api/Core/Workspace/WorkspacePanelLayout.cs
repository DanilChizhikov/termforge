namespace DTech.Core.Workspace;

public sealed record WorkspacePanelLayout(string GroupId, IReadOnlyDictionary<string, double> Sizes);
