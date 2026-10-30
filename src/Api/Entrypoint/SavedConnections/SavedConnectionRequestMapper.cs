using DTech.Core.Reconnect;
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

        var reconnectPolicy = ReadReconnectPolicy(request);
        if (!reconnectPolicy.IsSuccess)
        {
            return SavedConnectionResult<SavedConnectionSubmission>.Failure(reconnectPolicy.Error);
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
            ReconnectPolicy = reconnectPolicy.Value,
            ReadOnly = request.ReadOnly ?? false,
            AiAccess = request.AiAccess ?? false,
        };
        var submission = new SavedConnectionSubmission(draft, request.Password);
        request.Password = null;
        return SavedConnectionResult<SavedConnectionSubmission>.Success(submission);
    }

    private static SavedConnectionResult<ReconnectPolicy> ReadReconnectPolicy(SavedConnectionRequest request)
    {
        var defaults = ReconnectPolicy.Default;
        var mode = defaults.Mode;
        if (request.ReconnectMode is not null && !Enum.TryParse(request.ReconnectMode, true, out mode))
        {
            var unknown = SavedConnectionError.Invalid($"The reconnect mode '{request.ReconnectMode}' is not supported.");
            return SavedConnectionResult<ReconnectPolicy>.Failure(unknown);
        }

        var maxAttempts = request.ReconnectMaxAttempts ?? defaults.MaxAttempts;
        var initialDelay = request.ReconnectInitialDelaySeconds ?? defaults.InitialDelaySeconds;
        var maxDelay = request.ReconnectMaxDelaySeconds ?? defaults.MaxDelaySeconds;
        var policy = new ReconnectPolicy(mode, maxAttempts, initialDelay, maxDelay);
        return SavedConnectionResult<ReconnectPolicy>.Success(policy);
    }
}
