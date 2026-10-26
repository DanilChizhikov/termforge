namespace DTech.Core.CommandHistory;

public static class DefaultCommandExclusionPatterns
{
    public static IReadOnlyList<string> Values { get; } =
    [
        @"(?i)\b(password|passwd|passphrase|secret|token|api[_-]?key|access[_-]?key|private[_-]?key)\b",
        @"(?i)\bexport\s+\w*(key|token|secret|password|passwd)\w*=",
        @"(?i)\b\w*(key|token|secret|password|passwd)\w*=\S",
        @"(?i)authorization:\s*(bearer|basic)\s",
        @"(?i)://[^/\s:@]+:[^/\s@]+@",
        @"(?i)^(mysql|mysqldump|mysqladmin|mariadb)\b.*\s-p\S",
    ];
}
