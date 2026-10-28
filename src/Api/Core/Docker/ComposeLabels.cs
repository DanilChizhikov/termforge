namespace DTech.Core.Docker;

public sealed record ComposeLabels(string Project, string WorkingDirectory, IReadOnlyList<string> ConfigFiles);
