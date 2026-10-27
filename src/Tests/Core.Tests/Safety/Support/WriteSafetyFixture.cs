using DTech.Core.Safety;
using NSubstitute;

namespace DTech.Core.Tests.Safety.Support;

internal sealed class WriteSafetyFixture
{
    public WriteSafetyFixture()
    {
        AccessModes = Substitute.For<IServerAccessModeReader>();
        Confirmations = new ConfirmationTokenStore(TimeProvider.System);
        Gate = new ServerWriteGate(AccessModes, Confirmations);
    }

    public IServerAccessModeReader AccessModes { get; }

    public ConfirmationTokenStore Confirmations { get; }

    public ServerWriteGate Gate { get; }

    public void MarkReadOnly(Guid connectionId)
    {
        AccessModes.IsReadOnlyAsync(connectionId, Arg.Any<CancellationToken>()).Returns(true);
    }

    public string Confirm(DangerousOperation operation, Guid connectionId, string target)
    {
        var scope = new ConfirmationScope(operation, connectionId, target);
        var grant = Confirmations.Issue(scope);
        return grant.Token;
    }
}
