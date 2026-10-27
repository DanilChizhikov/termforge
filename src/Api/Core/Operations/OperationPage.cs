namespace DTech.Core.Operations;

public sealed record OperationPage(IReadOnlyList<Operation> Items, int Total, int Offset, int Limit);
