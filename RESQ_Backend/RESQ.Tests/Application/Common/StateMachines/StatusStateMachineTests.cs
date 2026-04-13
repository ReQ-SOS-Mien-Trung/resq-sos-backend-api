using RESQ.Application.Common.StateMachines;
using RESQ.Application.Exceptions;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Tests.Application.Common.StateMachines;

public class StatusStateMachineTests
{
    [Theory]
    [InlineData(MissionStatus.Planned, MissionStatus.OnGoing)]
    [InlineData(MissionStatus.OnGoing, MissionStatus.Completed)]
    [InlineData(MissionStatus.OnGoing, MissionStatus.Incompleted)]
    public void MissionStateMachine_DoesNotThrow_ForAllowedTransitions(MissionStatus from, MissionStatus to)
    {
        var exception = Record.Exception(() => MissionStateMachine.EnsureValidTransition(from, to));

        Assert.Null(exception);
    }

    [Theory]
    [InlineData(MissionStatus.Planned, MissionStatus.Completed)]
    [InlineData(MissionStatus.Planned, MissionStatus.Incompleted)]
    [InlineData(MissionStatus.OnGoing, MissionStatus.Planned)]
    [InlineData(MissionStatus.Completed, MissionStatus.OnGoing)]
    [InlineData(MissionStatus.Incompleted, MissionStatus.OnGoing)]
    public void MissionStateMachine_ThrowsBadRequest_ForInvalidTransitions(MissionStatus from, MissionStatus to)
    {
        var exception = Assert.Throws<BadRequestException>(
            () => MissionStateMachine.EnsureValidTransition(from, to));

        Assert.Contains($"'{from}'", exception.Message, StringComparison.Ordinal);
        Assert.Contains($"'{to}'", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(MissionActivityStatus.Planned, MissionActivityStatus.OnGoing)]
    [InlineData(MissionActivityStatus.Planned, MissionActivityStatus.Cancelled)]
    [InlineData(MissionActivityStatus.OnGoing, MissionActivityStatus.Succeed)]
    [InlineData(MissionActivityStatus.OnGoing, MissionActivityStatus.Failed)]
    [InlineData(MissionActivityStatus.OnGoing, MissionActivityStatus.Cancelled)]
    [InlineData(MissionActivityStatus.OnGoing, MissionActivityStatus.PendingConfirmation)]
    [InlineData(MissionActivityStatus.PendingConfirmation, MissionActivityStatus.Succeed)]
    [InlineData(MissionActivityStatus.PendingConfirmation, MissionActivityStatus.Failed)]
    [InlineData(MissionActivityStatus.PendingConfirmation, MissionActivityStatus.Cancelled)]
    public void MissionActivityStateMachine_DoesNotThrow_ForAllowedTransitions(
        MissionActivityStatus from,
        MissionActivityStatus to)
    {
        var exception = Record.Exception(() => MissionActivityStateMachine.EnsureValidTransition(from, to));

        Assert.Null(exception);
    }

    [Theory]
    [InlineData(MissionActivityStatus.Planned, MissionActivityStatus.Succeed)]
    [InlineData(MissionActivityStatus.OnGoing, MissionActivityStatus.Planned)]
    [InlineData(MissionActivityStatus.PendingConfirmation, MissionActivityStatus.OnGoing)]
    [InlineData(MissionActivityStatus.Succeed, MissionActivityStatus.OnGoing)]
    [InlineData(MissionActivityStatus.Failed, MissionActivityStatus.OnGoing)]
    [InlineData(MissionActivityStatus.Cancelled, MissionActivityStatus.OnGoing)]
    public void MissionActivityStateMachine_ThrowsBadRequest_ForInvalidTransitions(
        MissionActivityStatus from,
        MissionActivityStatus to)
    {
        var exception = Assert.Throws<BadRequestException>(
            () => MissionActivityStateMachine.EnsureValidTransition(from, to));

        Assert.Contains($"'{from}'", exception.Message, StringComparison.Ordinal);
        Assert.Contains($"'{to}'", exception.Message, StringComparison.Ordinal);
    }
}
