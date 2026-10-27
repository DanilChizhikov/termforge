namespace DTech.Core.SettingsTransfer.Document;

public sealed record ExportedPinnedCommand
{
    public Guid ConnectionId { get; init; }

    public string? Command { get; init; }
}
