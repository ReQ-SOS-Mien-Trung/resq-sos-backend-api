using RESQ.Application.Services;
using RESQ.Application.UseCases.Operations.Commands.UpdateMissionPendingActivities;

namespace RESQ.Tests.Application.UseCases.Operations.Commands;

public class UpdateMissionPendingActivitiesCommandValidatorTests
{
    private readonly UpdateMissionPendingActivitiesCommandValidator _validator = new();

    [Fact]
    public void Validate_ReturnsError_WhenActivityIdsAreDuplicated()
    {
        var command = new UpdateMissionPendingActivitiesCommand(
            15,
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            [
                new UpdateMissionPendingActivityPatch(7, 1, null, null, null, null, null),
                new UpdateMissionPendingActivityPatch(7, 2, null, null, null, null, null)
            ]);

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.ErrorMessage.Contains("ActivityId trùng", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_ReturnsError_WhenActivityHasNoChangesOrIncompleteCoordinates()
    {
        var command = new UpdateMissionPendingActivitiesCommand(
            15,
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            [
                new UpdateMissionPendingActivityPatch(7, null, null, null, null, null, null),
                new UpdateMissionPendingActivityPatch(8, null, "updated", null, 10.1, null, null)
            ]);

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.ErrorMessage.Contains("ít nhất một thay đổi", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Errors, error => error.ErrorMessage.Contains("TargetLatitude và TargetLongitude", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_ReturnsError_WhenItemPayloadIsInvalid()
    {
        var command = new UpdateMissionPendingActivitiesCommand(
            15,
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            [
                new UpdateMissionPendingActivityPatch(
                    7,
                    null,
                    null,
                    null,
                    null,
                    null,
                    [
                        new SupplyToCollectDto { ItemId = 5, Quantity = 2 },
                        new SupplyToCollectDto { ItemId = 5, Quantity = 1 },
                        new SupplyToCollectDto { ItemId = 6, Quantity = 0 }
                    ])
            ]);

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.ErrorMessage.Contains("ItemId trùng", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Errors, error => error.ErrorMessage.Contains("Quantity phải lớn hơn 0", StringComparison.OrdinalIgnoreCase));
    }
}