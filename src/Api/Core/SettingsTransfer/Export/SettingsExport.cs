namespace DTech.Core.SettingsTransfer.Export;

public sealed record SettingsExport
{
    public required string SettingsFileName { get; init; }

    public required string SettingsContent { get; init; }

    public string? SecretsFileName { get; init; }

    public byte[]? SecretsArchive { get; init; }

    public int SecretCount { get; init; }
}
