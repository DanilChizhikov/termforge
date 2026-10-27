namespace DTech.Entrypoint.Safety;

public sealed record ConfirmationResponse(string Token, DateTimeOffset ExpiresAt, string Operation, string ServerName, string Target);
