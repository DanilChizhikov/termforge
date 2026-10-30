using DTech.Core.AiBridge.Settings;

namespace DTech.Core.Tests.AiBridge.Support;

internal sealed class SequentialTokenGenerator : IAiBridgeTokenGenerator
{
    private int _next;

    public string Create()
    {
        _next++;
        return $"token-{_next}";
    }
}
