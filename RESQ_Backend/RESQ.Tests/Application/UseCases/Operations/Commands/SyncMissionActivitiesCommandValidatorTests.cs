using RESQ.Application.UseCases.Operations.Commands.SyncMissionActivities;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Tests.Application.UseCases.Operations.Commands;

public class SyncMissionActivitiesCommandValidatorTests
{
    private readonly SyncMissionActivitiesCommandValidator _validator = new();

    [Fact]
    public void Validate_Fails_WhenBatchIsEmpty()
    {
        var result = _validator.Validate(new SyncMissionActivitiesCommand(Guid.NewGuid(), []));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "Items");
    }

    [Fact]
    public void Validate_Fails_WhenBatchExceeds100Items()
    {
        var items = Enumerable.Range(1, 101)
            .Select(index => CreateItem(index, Guid.NewGuid()))
            .ToList();

        var result = _validator.Validate(new SyncMissionActivitiesCommand(Guid.NewGuid(), items));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "Items");
    }

    [Fact]
    public void Validate_Fails_WhenClientMutationIdsAreDuplicated()
    {
        var clientMutationId = Guid.NewGuid();
        var command = new SyncMissionActivitiesCommand(
            Guid.NewGuid(),
            [
                CreateItem(1, clientMutationId),
                CreateItem(2, clientMutationId)
            ]);

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.ErrorMessage.Contains("clientMutationId", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_Fails_WhenMissionActivityPairsAreDuplicated()
    {
        var first = CreateItem(1, Guid.NewGuid());
        var second = CreateItem(1, Guid.NewGuid());
        second.BaseServerStatus = MissionActivityStatus.OnGoing;

        var result = _validator.Validate(new SyncMissionActivitiesCommand(Guid.NewGuid(), [first, second]));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.ErrorMessage.Contains("missionId/activityId", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_Fails_WhenQueuedAtIsDefaultOrEnumIsOutOfRange()
    {
        var invalidItem = CreateItem(1, Guid.NewGuid());
        invalidItem.QueuedAt = default;
        invalidItem.TargetStatus = (MissionActivityStatus)999;

        var result = _validator.Validate(new SyncMissionActivitiesCommand(Guid.NewGuid(), [invalidItem]));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName.EndsWith("QueuedAt", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.PropertyName.EndsWith("TargetStatus", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_Passes_WhenImageUrlIsNull()
    {
        var result = _validator.Validate(new SyncMissionActivitiesCommand(Guid.NewGuid(), [CreateItem(1, Guid.NewGuid(), null)]));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_Fails_WhenImageUrlIsNotAbsolute()
    {
        var result = _validator.Validate(new SyncMissionActivitiesCommand(Guid.NewGuid(), [CreateItem(1, Guid.NewGuid(), "/proof.png")]));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName.EndsWith(nameof(MissionActivitySyncItemDto.ImageUrl), StringComparison.Ordinal));
    }

    private static MissionActivitySyncItemDto CreateItem(int activityId, Guid clientMutationId, string? imageUrl = "https://cdn.example.com/proof.jpg") => new()
    {
        ClientMutationId = clientMutationId,
        MissionId = 77,
        ActivityId = activityId,
        TargetStatus = MissionActivityStatus.OnGoing,
        BaseServerStatus = MissionActivityStatus.Planned,
        QueuedAt = new DateTimeOffset(2026, 4, 10, 9, 0, 0, TimeSpan.Zero).AddMinutes(activityId),
        ImageUrl = imageUrl
    };
}
