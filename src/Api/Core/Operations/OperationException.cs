namespace DTech.Core.Operations;

public sealed class OperationException : Exception
{
    public OperationException(OperationFailure failure, string message)
        : base(message)
    {
        Failure = failure;
    }

    public OperationFailure Failure { get; }
}
