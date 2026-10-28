namespace DTech.Core.Docker;

public static class ComposeLabelParser
{
    public const string ProjectLabel = "com.docker.compose.project";
    public const string WorkingDirectoryLabel = "com.docker.compose.project.working_dir";
    public const string ConfigFilesLabel = "com.docker.compose.project.config_files";

    private const int PathMaxLength = 1024;

    public static ComposeLabels? Parse(string labels)
    {
        var values = SplitLabels(labels);
        if (!values.TryGetValue(ProjectLabel, out var project) || !ComposeProjectName.TryParse(project, out _))
        {
            return null;
        }

        var workingDirectory = values.GetValueOrDefault(WorkingDirectoryLabel, string.Empty);
        var configFiles = values.GetValueOrDefault(ConfigFilesLabel, string.Empty);
        var files = SplitFiles(configFiles);
        var safeWorkingDirectory = IsSafePath(workingDirectory) ? workingDirectory : string.Empty;
        return new ComposeLabels(project, safeWorkingDirectory, files);
    }

    private static Dictionary<string, string> SplitLabels(string labels)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var segments = labels.Split(',');
        string? currentKey = null;
        for (var i = 0; i < segments.Length; i++)
        {
            var segment = segments[i];
            var continuesConfigFiles = currentKey == ConfigFilesLabel && segment.StartsWith('/');
            if (continuesConfigFiles)
            {
                values[ConfigFilesLabel] = $"{values[ConfigFilesLabel]},{segment}";
                continue;
            }

            var separatorIndex = segment.IndexOf('=', StringComparison.Ordinal);
            if (separatorIndex <= 0)
            {
                currentKey = null;
                continue;
            }

            currentKey = segment[..separatorIndex].Trim();
            values[currentKey] = segment[(separatorIndex + 1)..];
        }

        return values;
    }

    private static IReadOnlyList<string> SplitFiles(string configFiles)
    {
        var parts = configFiles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var files = new List<string>(parts.Length);
        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            if (IsSafePath(part))
            {
                files.Add(part);
            }
        }

        return files;
    }

    private static bool IsSafePath(string path)
    {
        if (path.Length == 0 || path.Length > PathMaxLength || path[0] != '/')
        {
            return false;
        }

        for (var i = 0; i < path.Length; i++)
        {
            if (char.IsControl(path[i]))
            {
                return false;
            }
        }

        return true;
    }
}
