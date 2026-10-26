using DTech.Core.SavedConnections;

namespace DTech.Entrypoint.SavedConnections;

internal static class SavedConnectionRequestMapper
{
    public static SavedConnectionResult<SavedConnectionSubmission> ToSubmission(SavedConnectionRequest request)
    {
        if (request.Name is null || request.Host is null || request.UserName is null)
        {
            var missing = SavedConnectionError.Invalid("The name, host and user name are required.");
            return SavedConnectionResult<SavedConnectionSubmission>.Failure(missing);
        }

        var authMethod = ConnectionAuthMethod.Agent;
        if (request.AuthMethod is not null && !Enum.TryParse(request.AuthMethod, true, out authMethod))
        {
            var unknown = SavedConnectionError.Invalid($"The authentication method '{request.AuthMethod}' is not supported.");
            return SavedConnectionResult<SavedConnectionSubmission>.Failure(unknown);
        }

        var draft = new SavedConnectionDraft
        {
            Name = request.Name,
            Host = request.Host,
            Port = request.Port ?? 22,
            UserName = request.UserName,
            AuthMethod = authMethod,
            GroupName = request.GroupName,
            PrivateKeyPath = request.PrivateKeyPath,
            JumpHostId = request.JumpHostId,
            SshAlias = request.SshAlias,
        };
        var submission = new SavedConnectionSubmission(draft, request.Password);
        request.Password = null;
        return SavedConnectionResult<SavedConnectionSubmission>.Success(submission);
    }
}
