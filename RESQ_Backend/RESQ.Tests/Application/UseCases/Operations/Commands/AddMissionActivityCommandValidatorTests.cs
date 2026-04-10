using RESQ.Application.UseCases.Operations.Commands.AddMissionActivity;

namespace RESQ.Tests.Application.UseCases.Operations.Commands;

public class AddMissionActivityCommandValidatorTests
{
    private readonly AddMissionActivityCommandValidator _validator = new();

    [Fact]
    public void Validate_Fails_WhenMissionIdIsNotPositive()
    {
        var result = _validator.Validate(BuildCommand(missionId: 0));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(AddMissionActivityCommand.MissionId));
    }

    [Fact]
    public void Validate_Fails_WhenActivityTypeIsMissing()
    {
        var result = _validator.Validate(BuildCommand(activityType: null));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(AddMissionActivityCommand.ActivityType));
    }

    [Fact]
    public void Validate_Fails_WhenRescueTeamIdIsNotPositive()
    {
        var result = _validator.Validate(BuildCommand(rescueTeamId: 0));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.ErrorMessage.Contains("RescueTeamId", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_Passes_WhenCommandIsValid()
    {
        var result = _validator.Validate(BuildCommand());

        Assert.True(result.IsValid);
    }

    private static AddMissionActivityCommand BuildCommand(int missionId = 1, string? activityType = "EVACUATE", int? rescueTeamId = 2)
        => new(
            missionId,
            1,
            activityType,
            "Sơ tán nạn nhân",
            "High",
            30,
            null,
            null,
            null,
            null,
            null,
            "Target",
            10.0,
            106.0,
            rescueTeamId,
            Guid.NewGuid());
}