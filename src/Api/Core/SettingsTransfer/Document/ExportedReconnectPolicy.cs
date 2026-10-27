namespace DTech.Core.SettingsTransfer.Document;

public sealed record ExportedReconnectPolicy
{
    public string? Mode { get; init; }

    public int MaxAttempts { get; init; }

    public int InitialDelaySeconds { get; init; }

    public int MaxDelaySeconds { get; init; }
}
