namespace DTech.Core.Operations;

public readonly record struct OperationQuery(
    string? Kind,
    OperationStatus? Status,
    string? Server,
    DateTime? From,
    DateTime? To,
    int Offset,
    int Limit)
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 200;

    public static OperationQuery Unfiltered => new(null, null, null, null, null, 0, DefaultLimit);

    public OperationQuery Normalized()
    {
        var offset = Math.Max(0, Offset);
        var limit = Limit <= 0 ? DefaultLimit : Math.Min(Limit, MaxLimit);
        return this with { Offset = offset, Limit = limit };
    }
}
