namespace DTech.Persistence.Database;

public sealed class PersistenceOptions
{
    public const string SectionName = "Persistence";

    public string? DatabasePath { get; set; }
}
