using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Operations.Commands.ConfirmMissionSupplyPickup;
using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Enum.Operations;
using RESQ.Tests.TestDoubles;

namespace RESQ.Tests.Application.UseCases.Operations.Commands;

public class ConfirmMissionSupplyPickupCommandHandlerTests
{
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    // ─── Activity not found ───────────────────────────────────────

    [Fact]
    public async Task Handle_ThrowsNotFound_WhenActivityDoesNotExist()
    {
        var handler = BuildHandler(activityRepo: new StubActivityRepo(null));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(BuildCommand(), CancellationToken.None));
    }

    // ─── Activity has no items ────────────────────────────────────

    [Fact]
    public async Task Handle_ThrowsBadRequest_WhenActivityHasNoItems()
    {
        var activity = BuildActivity(items: "");
        var handler = BuildHandler(activityRepo: new StubActivityRepo(activity));

        await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(BuildCommand(), CancellationToken.None));
    }

    // ─── Buffer usage without reason ──────────────────────────────

    [Fact]
    public async Task Handle_ThrowsBadRequest_WhenBufferUsageLacksReason()
    {
        var activity = BuildActivity();
        var handler = BuildHandler(activityRepo: new StubActivityRepo(activity));

        var command = new ConfirmMissionSupplyPickupCommand(
            ActivityId: 1,
            MissionId: 10,
            UserId: UserId,
            BufferUsages:
            [
                new MissionPickupBufferUsageDto { ItemId = 100, BufferQuantityUsed = 2, BufferUsedReason = null }
            ]);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(command, CancellationToken.None));
    }

    // ─── Item not in activity ─────────────────────────────────────

    [Fact]
    public async Task Handle_ThrowsBadRequest_WhenItemNotInActivity()
    {
        var activity = BuildActivity();
        var handler = BuildHandler(activityRepo: new StubActivityRepo(activity));

        var command = new ConfirmMissionSupplyPickupCommand(
            ActivityId: 1,
            MissionId: 10,
            UserId: UserId,
            BufferUsages:
            [
                new MissionPickupBufferUsageDto { ItemId = 999, BufferQuantityUsed = 1, BufferUsedReason = "Need more" }
            ]);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(command, CancellationToken.None));
    }

    // ─── Buffer exceeds allocated ─────────────────────────────────

    [Fact]
    public async Task Handle_ThrowsBadRequest_WhenBufferExceedsAllocated()
    {
        var activity = BuildActivity();
        var handler = BuildHandler(activityRepo: new StubActivityRepo(activity));

        var command = new ConfirmMissionSupplyPickupCommand(
            ActivityId: 1,
            MissionId: 10,
            UserId: UserId,
            BufferUsages:
            [
                new MissionPickupBufferUsageDto { ItemId = 100, BufferQuantityUsed = 50, BufferUsedReason = "Too much" }
            ]);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(command, CancellationToken.None));
    }

    // ─── Success syncs buffer usage ───────────────────────────────

    [Fact]
    public async Task Handle_Success_SyncsBufferUsage()
    {
        var activity = BuildActivity();
        var uow = new StubUnitOfWork();
        var handler = BuildHandler(activityRepo: new StubActivityRepo(activity), uow: uow);

        var command = new ConfirmMissionSupplyPickupCommand(
            ActivityId: 1,
            MissionId: 10,
            UserId: UserId,
            BufferUsages:
            [
                new MissionPickupBufferUsageDto { ItemId = 100, BufferQuantityUsed = 1, BufferUsedReason = "Damage in transit" }
            ]);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(1, result.ActivityId);
        Assert.Equal(10, result.MissionId);
        Assert.Contains("buffer", result.Message);
        Assert.True(uow.SaveCalls > 0);
    }

    // ─── No buffer usage → informational message ──────────────────

    [Fact]
    public async Task Handle_NoBufferUsage_ReturnsInfoMessage()
    {
        var activity = BuildActivity();
        var handler = BuildHandler(activityRepo: new StubActivityRepo(activity));

        var command = new ConfirmMissionSupplyPickupCommand(
            ActivityId: 1,
            MissionId: 10,
            UserId: UserId,
            BufferUsages: null);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Contains("Không có buffer", result.Message);
    }

    // ─── Helpers ──────────────────────────────────────────────────

    private static ConfirmMissionSupplyPickupCommand BuildCommand() =>
        new(ActivityId: 1, MissionId: 10, UserId: UserId, BufferUsages: null);

    private static MissionActivityModel BuildActivity(string? items = null) => new()
    {
        Id = 1,
        MissionId = 10,
        ActivityType = "COLLECT_SUPPLIES",
        Status = MissionActivityStatus.OnGoing,
        Items = items ?? """[{"ItemId":100,"ItemName":"Water","Quantity":10,"BufferQuantity":2}]"""
    };

    private static ConfirmMissionSupplyPickupCommandHandler BuildHandler(
        StubActivityRepo? activityRepo = null,
        StubUnitOfWork? uow = null)
    {
        return new ConfirmMissionSupplyPickupCommandHandler(
            activityRepo ?? new StubActivityRepo(BuildActivity()),
            uow ?? new StubUnitOfWork());
    }

    // ─── Stubs ────────────────────────────────────────────────────

    private sealed class StubActivityRepo(MissionActivityModel? activity) : IMissionActivityRepository
    {
        public Task<MissionActivityModel?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult(activity);
        public Task<int> AddAsync(MissionActivityModel a, CancellationToken ct = default) => Task.FromResult(a.Id);
        public Task UpdateAsync(MissionActivityModel a, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IEnumerable<MissionActivityModel>> GetByMissionIdAsync(int mid, CancellationToken ct = default) => Task.FromResult(Enumerable.Empty<MissionActivityModel>());
        public Task<IEnumerable<MissionActivityModel>> GetBySosRequestIdsAsync(IEnumerable<int> ids, CancellationToken ct = default) => Task.FromResult(Enumerable.Empty<MissionActivityModel>());
        public Task<IReadOnlyList<MissionActivityModel>> GetOpenByAssemblyPointAsync(int apId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<MissionActivityModel>>([]);
        public Task UpdateStatusAsync(int aid, MissionActivityStatus s, Guid db, string? img = null, CancellationToken ct = default) => Task.CompletedTask;
        public Task AssignTeamAsync(int aid, int mtid, CancellationToken ct = default) => Task.CompletedTask;
        public Task ResetAssignmentsToPlannedAsync(IEnumerable<int> aids, Guid db, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(int id, CancellationToken ct = default) => Task.CompletedTask;
    }
}
