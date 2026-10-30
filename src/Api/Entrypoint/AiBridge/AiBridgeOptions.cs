namespace DTech.Entrypoint.AiBridge;

internal sealed class AiBridgeOptions
{
    public const string SectionName = "AiBridge";

    public int ApprovalTimeoutSeconds { get; set; } = 120;
}
