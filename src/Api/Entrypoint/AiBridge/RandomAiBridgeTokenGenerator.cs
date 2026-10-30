using DTech.Core.AiBridge.Settings;
using DTech.Entrypoint.Security;

namespace DTech.Entrypoint.AiBridge;

internal sealed class RandomAiBridgeTokenGenerator : IAiBridgeTokenGenerator
{
    public string Create()
    {
        return RandomSecret.Create();
    }
}
