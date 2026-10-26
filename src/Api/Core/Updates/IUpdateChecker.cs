namespace DTech.Core.Updates;

public interface IUpdateChecker
{
    UpdateStatus Peek();

    Task<UpdateStatus> Check(bool force, CancellationToken cancellationToken);
}
