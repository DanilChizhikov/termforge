namespace DTech.Core.Operations;

public enum OperationFailure
{
    NotFound,
    InvalidTransition,
    InvalidRequest,
    NotRetryable,
    NoRetryHandler,
}
