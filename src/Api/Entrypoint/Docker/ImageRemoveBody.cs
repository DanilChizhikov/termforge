namespace DTech.Entrypoint.Docker;

internal sealed record ImageRemoveBody(string? Image, string? ConfirmationToken);
