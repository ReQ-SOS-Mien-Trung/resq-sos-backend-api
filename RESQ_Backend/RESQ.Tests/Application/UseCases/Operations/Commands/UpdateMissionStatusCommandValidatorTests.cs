using RESQ.Application.UseCases.Operations.Commands.UpdateMissionStatus;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Tests.Application.UseCases.Operations.Commands;

public class UpdateMissionStatusCommandValidatorTests
{
    private static readonly Guid DecisionById = Guid.Parse("aaaaaaaa-3333-3333-3333-333333333333");

    private readonly UpdateMissionStatusCommandValidator _validator = new();

    [Fact]
    public void Validate_Passes_WhenCommandIsValid()
    {
        var result = _validator.Validate(BuildCommand(status: MissionStatus.OnGoing));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_Fails_WhenMissionIdIsNotPositive()
    {
        var result = _validator.Validate(BuildCommand(missionId: 0));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(UpdateMissionStatusCommand.MissionId));
    }

    [Fact]
    public void Validate_Fails_WhenStatusIsOutOfRange()
    {
        var result = _validator.Validate(BuildCommand(status: (MissionStatus)999));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(UpdateMissionStatusCommand.Status));
    }

    [Fact]
    public void Validate_Fails_WhenDecisionByIsEmpty()
    {
        var result = _validator.Validate(BuildCommand(decisionBy: Guid.Empty));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(UpdateMissionStatusCommand.DecisionBy));
    }

    private static UpdateMissionStatusCommand BuildCommand(
        int missionId = 12,
        MissionStatus status = MissionStatus.OnGoing,
        Guid? decisionBy = null)
        => new(missionId, status, decisionBy ?? DecisionById);
}
