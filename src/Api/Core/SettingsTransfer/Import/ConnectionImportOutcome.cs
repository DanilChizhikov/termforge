namespace DTech.Core.SettingsTransfer.Import;

public sealed class ConnectionImportOutcome
{
    public int Added { get; set; }

    public int Updated { get; set; }

    public int Unchanged { get; set; }

    public int Kept { get; set; }

    public int Removed { get; set; }

    public int SecretsStored { get; set; }

    public List<string> Warnings { get; } = [];
}
