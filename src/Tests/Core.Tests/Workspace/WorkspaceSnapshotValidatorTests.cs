using DTech.Core.Workspace;
using FluentAssertions;

namespace DTech.Core.Tests.Workspace;

[TestClass]
public sealed class WorkspaceSnapshotValidatorTests
{
    [TestMethod]
    public void Validate_StateWithDraftForAnOpenTab_IsValid()
    {
        var draft = WorkspaceSamples.Draft(WorkspaceSamples.WebId, "/etc/nginx/nginx.conf");
        var snapshot = new WorkspaceSnapshot(WorkspaceSamples.State(), [draft]);

        var error = WorkspaceSnapshotValidator.Validate(snapshot);

        error.Should().BeNull();
    }

    [TestMethod]
    public void Validate_DraftWithoutOpenTab_IsRejected()
    {
        var draft = WorkspaceSamples.Draft(WorkspaceSamples.WebId, "/etc/passwd");
        var snapshot = new WorkspaceSnapshot(WorkspaceSamples.State(), [draft]);

        var error = WorkspaceSnapshotValidator.Validate(snapshot);

        error.Should().NotBeNull();
    }

    [TestMethod]
    public void Validate_DraftLongerThanTheLimit_IsRejected()
    {
        var content = new string('x', WorkspaceStateLimits.MaxDraftCharacters + 1);
        var draft = WorkspaceSamples.Draft(WorkspaceSamples.WebId, "/etc/nginx/nginx.conf") with { Content = content };
        var snapshot = new WorkspaceSnapshot(WorkspaceSamples.State(), [draft]);

        var error = WorkspaceSnapshotValidator.Validate(snapshot);

        error!.Message.Should().Contain("longer than");
    }

    [TestMethod]
    public void Validate_MissingSnapshotOrState_IsRejected()
    {
        var withoutState = new WorkspaceSnapshot(null!, []);

        WorkspaceSnapshotValidator.Validate(null).Should().NotBeNull();
        WorkspaceSnapshotValidator.Validate(withoutState).Should().NotBeNull();
    }

    [TestMethod]
    public void Validate_StateBeyondLimits_IsRejected()
    {
        var tabs = Enumerable.Range(0, WorkspaceStateLimits.MaxTerminalTabs + 1)
            .Select(index => new WorkspaceTerminalTab($"terminal-{index}", WorkspaceSamples.WebId, "web"))
            .ToList();
        var tooManyTabs = WorkspaceSamples.State() with { TerminalTabs = tabs };
        var badPath = WorkspaceSamples.State() with { LocalPath = "/tmp/\u0000evil" };
        var badView = WorkspaceSamples.State() with { ActiveView = "../settings" };

        WorkspaceStateValidator.Validate(tooManyTabs).Should().NotBeNull();
        WorkspaceStateValidator.Validate(badPath).Should().NotBeNull();
        WorkspaceStateValidator.Validate(badView).Should().NotBeNull();
    }
}
