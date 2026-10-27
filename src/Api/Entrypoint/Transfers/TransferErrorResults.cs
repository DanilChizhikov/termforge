namespace DTech.Entrypoint.Transfers;

internal static class TransferErrorResults
{
    public const string ConflictCode = "TransferConflict";

    public static IResult Conflict(IReadOnlyList<string> conflicts)
    {
        var extensions = new Dictionary<string, object?>
        {
            ["code"] = ConflictCode,
            ["conflicts"] = conflicts,
        };
        return Results.Problem(
            detail: $"{conflicts.Count} target item(s) already exist. Confirm overwriting to continue.",
            statusCode: StatusCodes.Status409Conflict,
            extensions: extensions);
    }

    public static IResult Invalid(string message)
    {
        var extensions = new Dictionary<string, object?>
        {
            ["code"] = "Invalid",
        };
        return Results.Problem(detail: message, statusCode: StatusCodes.Status400BadRequest, extensions: extensions);
    }

    public static IResult NotFound(Guid id)
    {
        var extensions = new Dictionary<string, object?>
        {
            ["code"] = "NotFound",
        };
        return Results.Problem(detail: $"Transfer {id} was not found.", statusCode: StatusCodes.Status404NotFound, extensions: extensions);
    }
}
