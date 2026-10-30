using DTech.Core.AiBridge;
using ModelContextProtocol;

namespace DTech.Entrypoint.AiBridge.Tools;

internal static class AiToolResults
{
    public static T ValueOrThrow<T>(AiBridgeResult<T> result)
    {
        if (!result.IsSuccess)
        {
            throw new McpException(result.Error.Message);
        }

        return result.Value;
    }
}
