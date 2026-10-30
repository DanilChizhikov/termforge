using DTech.Core.CommandHistory;

namespace DTech.Core.SettingsTransfer;

public static class SettingsTransferLimits
{
    public const int MaxSettingsFileCharacters = 4 * 1024 * 1024;
    public const int MaxPackageBytes = 16 * 1024 * 1024;
    public const int MaxConnections = 1000;
    public const int MaxPinnedCommands = 5000;
    public const int MaxPinnedCommandsPerConnection = CommandHistoryLimits.MaxPageSize;
    public const int MaxSecretBytes = 16 * 1024;
    public const int MaxKdfIterations = 10_000_000;
}
