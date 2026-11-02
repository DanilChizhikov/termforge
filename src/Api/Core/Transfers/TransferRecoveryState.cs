using System.Text.Json;
using DTech.Core.Operations;

namespace DTech.Core.Transfers;

public sealed record TransferRecoveryState(TransferRequest Request, string TargetPath, IReadOnlyList<string> PartialPaths)
{
    public long PartialBytes { get; init; }

    public static TransferRecoveryState? TryRead(Operation operation)
    {
        if (operation.RecoveryData is null)
        {
            return null;
        }

        try
        {
            var state = JsonSerializer.Deserialize<TransferRecoveryState>(operation.RecoveryData);
            if (state?.Request is null || state.PartialPaths is null
                || state.Request.ConnectionId == Guid.Empty || !Enum.IsDefined(state.Request.Direction)
                || string.IsNullOrWhiteSpace(state.Request.SourcePath)
                || string.IsNullOrWhiteSpace(state.Request.TargetDirectory)
                || string.IsNullOrWhiteSpace(state.TargetPath)
                || state.PartialBytes < 0
                || state.Request.SourcePath.Contains('\0')
                || state.Request.TargetDirectory.Contains('\0')
                || state.TargetPath.Contains('\0')
                || operation.Kind != TransferOperationKinds.For(state.Request.Direction))
            {
                return null;
            }

            for (var i = 0; i < state.PartialPaths.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(state.PartialPaths[i]) || state.PartialPaths[i].Contains('\0'))
                {
                    return null;
                }
            }

            return state;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
