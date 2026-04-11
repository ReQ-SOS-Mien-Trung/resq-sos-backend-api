using RESQ.Application.Services;
using RESQ.Application.UseCases.Operations.Commands.UpdateMission;

namespace RESQ.Tests.Application.UseCases.Operations.Commands;

public class UpdateMissionCommandValidatorTests
{
    private readonly UpdateMissionCommandValidator _validator = new();

    [Fact]
    public void Validate_ReturnsError_WhenActivitiesAreDuplicated()
    {
        var command = new UpdateMissionCommand(
            15,
            "Medical",
            80,
            DateTime.UtcNow,
            DateTime.UtcNow.AddHours(1),
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            [
                new UpdateMissionActivityPatch(7, 1, null, null, null, null, null),
                new UpdateMissionActivityPatch(7, 2, null, null, null, null, null)
            ]);

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.ErrorMessage.Contains("ActivityId trùng", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_ReturnsError_WhenActivitiesAreProvidedWithoutUpdatedBy()
    {
        var command = new UpdateMissionCommand(
            15,
            "Medical",
            80,
            DateTime.UtcNow,
            DateTime.UtcNow.AddHours(1),
            null,
            [new UpdateMissionActivityPatch(7, 1, null, null, null, null, null)]);

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.ErrorMessage.Contains("UpdatedBy", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_ReturnsError_WhenActivityHasNoChangesOrIncompleteCoordinates()
    {
        var command = new UpdateMissionCommand(
            15,
            "Medical",
            80,
            DateTime.UtcNow,
            DateTime.UtcNow.AddHours(1),
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            [
                new UpdateMissionActivityPatch(7, null, null, null, null, null, null),
                new UpdateMissionActivityPatch(8, null, "updated", null, 10.1, null, null)
            ]);

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.ErrorMessage.Contains("ít nhất một thay đổi", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Errors, error => error.ErrorMessage.Contains("TargetLatitude và TargetLongitude", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_ReturnsError_WhenItemPayloadIsInvalid()
    {
        var command = new UpdateMissionCommand(
            15,
            "Medical",
            80,
            DateTime.UtcNow,
            DateTime.UtcNow.AddHours(1),
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            [
                new UpdateMissionActivityPatch(
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
