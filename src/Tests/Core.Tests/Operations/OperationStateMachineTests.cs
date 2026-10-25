using DTech.Core.Operations;
using FluentAssertions;

namespace DTech.Core.Tests.Operations;

[TestClass]
public sealed class OperationStateMachineTests
{
    [TestMethod]
    [DataRow(OperationStatus.Queued, OperationStatus.Running)]
    [DataRow(OperationStatus.Queued, OperationStatus.Cancelled)]
    [DataRow(OperationStatus.Running, OperationStatus.Succeeded)]
    [DataRow(OperationStatus.Running, OperationStatus.Failed)]
    [DataRow(OperationStatus.Running, OperationStatus.Cancelled)]
    public void Allowed_transitions_are_accepted(OperationStatus from, OperationStatus to)
    {
        OperationStateMachine.CanTransition(from, to).Should().BeTrue();
    }

    [TestMethod]
    [DataRow(OperationStatus.Queued, OperationStatus.Succeeded)]
    [DataRow(OperationStatus.Queued, OperationStatus.Failed)]
    [DataRow(OperationStatus.Queued, OperationStatus.Queued)]
    [DataRow(OperationStatus.Running, OperationStatus.Queued)]
    [DataRow(OperationStatus.Running, OperationStatus.Running)]
    [DataRow(OperationStatus.Succeeded, OperationStatus.Running)]
    [DataRow(OperationStatus.Succeeded, OperationStatus.Failed)]
    [DataRow(OperationStatus.Failed, OperationStatus.Running)]
    [DataRow(OperationStatus.Failed, OperationStatus.Succeeded)]
    [DataRow(OperationStatus.Cancelled, OperationStatus.Running)]
    [DataRow(OperationStatus.Cancelled, OperationStatus.Queued)]
    public void Invalid_transitions_are_rejected(OperationStatus from, OperationStatus to)
    {
        OperationStateMachine.CanTransition(from, to).Should().BeFalse();
    }

    [TestMethod]
    [DataRow(OperationStatus.Succeeded)]
    [DataRow(OperationStatus.Failed)]
    [DataRow(OperationStatus.Cancelled)]
    public void Terminal_states_have_no_outgoing_transitions(OperationStatus terminal)
    {
        OperationStateMachine.IsTerminal(terminal).Should().BeTrue();
        foreach (var target in Enum.GetValues<OperationStatus>())
        {
            OperationStateMachine.CanTransition(terminal, target).Should().BeFalse();
        }
    }
}
