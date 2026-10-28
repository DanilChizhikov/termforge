using DTech.Core.Safety;

namespace DTech.Core.Docker;

public sealed record ComposeActionRequest(Guid ConnectionId, string? Project, ComposeAction Action)
{
    public string? ConfirmationToken { get; init; }

    public DangerousOperation DangerousOperation => Action == ComposeAction.Restart
        ? DangerousOperation.ComposeRestart
        : DangerousOperation.ComposeRecreate;
}
