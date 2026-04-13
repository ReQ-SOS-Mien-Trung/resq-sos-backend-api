using RESQ.Application.UseCases.Operations.Commands.UpdateActivityStatus;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Tests.Application.UseCases.Operations.Commands;

public class UpdateActivityStatusCommandValidatorTests
{
    private static readonly Guid DecisionById = Guid.Parse("aaaaaaaa-2222-2222-2222-222222222222");

    private readonly UpdateActivityStatusCommandValidator _validator = new();

    [Fact]
    public void Validate_Passes_WhenCommandIsValidWithPendingConfirmation()
    {
        var result = _validator.Validate(BuildCommand(status: MissionActivityStatus.PendingConfirmation));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_Passes_WhenImageUrlIsNull()
    {
        var result = _validator.Validate(BuildCommand(imageUrl: null));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_Fails_WhenIdsAreNotPositive()
    {
        var result = _validator.Validate(BuildCommand(missionId: 0, activityId: 0));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(UpdateActivityStatusCommand.MissionId));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(UpdateActivityStatusCommand.ActivityId));
    }

    [Fact]
    public void Validate_Fails_WhenStatusIsOutOfRange()
    {
        var result = _validator.Validate(BuildCommand(status: (MissionActivityStatus)999));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(UpdateActivityStatusCommand.Status));
    }

    [Fact]
    public void Validate_Fails_WhenDecisionByIsEmpty()
    {
        var result = _validator.Validate(BuildCommand(decisionBy: Guid.Empty));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(UpdateActivityStatusCommand.DecisionBy));
    }

    [Fact]
    public void Validate_Fails_WhenImageUrlIsNotAbsolute()
    {
        var result = _validator.Validate(BuildCommand(imageUrl: "/images/proof.jpg"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(UpdateActivityStatusCommand.ImageUrl));
    }

    private static UpdateActivityStatusCommand BuildCommand(
        int missionId = 12,
        int activityId = 34,
        MissionActivityStatus status = MissionActivityStatus.Succeed,
        Guid? decisionBy = null,
        string? imageUrl = "https://cdn.example.com/proof.jpg")
        => new(missionId, activityId, status, decisionBy ?? DecisionById, imageUrl);
}
