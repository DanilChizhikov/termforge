namespace DTech.Core.Safety;

public sealed record ConfirmationIssueResult(ConfirmationIssueStatus Status, ConfirmationGrant? Grant, string? ServerName);
