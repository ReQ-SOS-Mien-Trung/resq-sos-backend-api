using RESQ.Application.UseCases.Operations.Commands.CreateMission;

namespace RESQ.Tests.Application.UseCases.Operations.Commands;

public class CreateMissionCommandValidatorTests
{
    private readonly CreateMissionCommandValidator _validator = new();

    [Fact]
    public void Validate_Fails_WhenClusterIdIsNotPositive()
    {
        var result = _validator.Validate(BuildCommand(clusterId: 0));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateMissionCommand.ClusterId));
    }

    [Fact]
    public void Validate_Fails_WhenCreatedByIdIsEmpty()
    {
        var result = _validator.Validate(BuildCommand(createdById: Guid.Empty));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateMissionCommand.CreatedById));
    }

    [Fact]
    public void Validate_Fails_WhenMissionTypeExceedsMaximumLength()
    {
        var result = _validator.Validate(BuildCommand(missionType: new string('x', 51)));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateMissionCommand.MissionType));
    }

    [Fact]
    public void Validate_Fails_WhenActivityRescueTeamIdIsNotPositive()
    {
        var result = _validator.Validate(BuildCommand(activities:
        [
            new CreateActivityItemDto
            {
                Step = 1,
                ActivityType = "EVACUATE",
                RescueTeamId = 0
            }
        ]));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.ErrorMessage.Contains("RescueTeamId", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_Passes_WhenCommandIsValid()
    {
        var result = _validator.Validate(BuildCommand());

        Assert.True(result.IsValid);
    }

    private static CreateMissionCommand BuildCommand(
        int clusterId = 1,
        Guid? createdById = null,
        string? missionType = "Mixed Rescue",
        List<CreateActivityItemDto>? activities = null)
        => new(
            clusterId,
            missionType,
            88.0,
            DateTime.UtcNow,
            DateTime.UtcNow.AddHours(4),
            activities ??
            [
                new CreateActivityItemDto
                {
                    Step = 1,
                    ActivityType = "EVACUATE",
                    RescueTeamId = 5
                }
            ],
            createdById ?? Guid.NewGuid());
}