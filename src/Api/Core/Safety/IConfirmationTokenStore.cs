namespace DTech.Core.Safety;

public interface IConfirmationTokenStore
{
    ConfirmationGrant Issue(ConfirmationScope scope);

    bool TryConsume(ConfirmationScope scope, string? token);
}
