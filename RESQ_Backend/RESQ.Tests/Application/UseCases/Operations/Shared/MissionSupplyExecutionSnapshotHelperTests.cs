using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Operations.Shared;
using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Tests.Application.UseCases.Operations.Shared;

public class MissionSupplyExecutionSnapshotHelperTests
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task RebuildExpectedReturnUnits_UsesPickedSnapshotWithoutFallingBackToPlannedUnits()
    {
        const int missionId = 7;
        const int depotId = 3;
        const int missionTeamId = 6;
        const int ropeItemId = 74;

        var staleCollectActivity = new MissionActivityModel
        {
            Id = 12,
            MissionId = missionId,
            MissionTeamId = missionTeamId,
            DepotId = depotId,
            ActivityType = "COLLECT_SUPPLIES",
            Items = SerializeSupplies([
                new SupplyToCollectDto
                {
                    ItemId = ropeItemId,
                    ItemName = "Rescue rope",
                    Quantity = 1,
                    Unit = "roll",
                    PlannedPickupReusableUnits =
                    [
                        Unit(267, ropeItemId, "D3-R074-001"),
                        Unit(268, ropeItemId, "D3-R074-002")
                    ]
                }
            ])
        };

        var collectActivityWithUnsavedPickedSnapshot = new MissionActivityModel
        {
            Id = staleCollectActivity.Id,
            MissionId = missionId,
            MissionTeamId = missionTeamId,
            DepotId = depotId,
            ActivityType = "COLLECT_SUPPLIES",
            Items = SerializeSupplies([
                new SupplyToCollectDto
                {
                    ItemId = ropeItemId,
                    ItemName = "Rescue rope",
                    Quantity = 1,
                    Unit = "roll",
                    PlannedPickupReusableUnits =
                    [
                        Unit(267, ropeItemId, "D3-R074-001"),
                        Unit(268, ropeItemId, "D3-R074-002")
                    ],
                    PickedReusableUnits =
                    [
                        Unit(267, ropeItemId, "D3-R074-001")
                    ]
                }
            ])
        };

        var otherCollectActivityWithoutPickup = new MissionActivityModel
        {
            Id = 20,
            MissionId = missionId,
            MissionTeamId = missionTeamId,
            DepotId = depotId,
            ActivityType = "COLLECT_SUPPLIES",
            Items = SerializeSupplies([
                new SupplyToCollectDto
                {
                    ItemId = ropeItemId,
                    ItemName = "Rescue rope",
                    Quantity = 1,
                    Unit = "roll",
                    PlannedPickupReusableUnits =
                    [
                        Unit(734, ropeItemId, "D3-R074-003")
                    ]
                }
            ])
        };

        var returnActivity = new MissionActivityModel
        {
            Id = 19,
            MissionId = missionId,
            MissionTeamId = missionTeamId,
            DepotId = depotId,
            ActivityType = "RETURN_SUPPLIES",
            Status = MissionActivityStatus.PendingConfirmation,
            Items = SerializeSupplies([
                new SupplyToCollectDto
                {
                    ItemId = ropeItemId,
                    ItemName = "Rescue rope",
                    Quantity = 1,
                    Unit = "roll",
                    ExpectedReturnUnits =
                    [
                        Unit(267, ropeItemId, "D3-R074-001"),
                        Unit(268, ropeItemId, "D3-R074-002"),
                        Unit(734, ropeItemId, "D3-R074-003")
                    ]
                }
            ])
        };

        var repository = new StubMissionActivityRepository([
            staleCollectActivity,
            otherCollectActivityWithoutPickup,
            returnActivity
        ]);

        await MissionSupplyExecutionSnapshotHelper.RebuildExpectedReturnUnitsAsync(
            collectActivityWithUnsavedPickedSnapshot,
            repository,
            NullLogger.Instance,
            CancellationToken.None);

        var returnItem = Assert.Single(DeserializeSupplies(returnActivity.Items!));
        var expectedUnit = Assert.Single(returnItem.ExpectedReturnUnits!);

        Assert.Equal(267, expectedUnit.ReusableItemId);
        Assert.Equal(1, repository.UpdateCalls);
    }

    private static SupplyExecutionReusableUnitDto Unit(int reusableItemId, int itemModelId, string serialNumber) => new()
    {
        ReusableItemId = reusableItemId,
        ItemModelId = itemModelId,
        ItemName = "Rescue rope",
        SerialNumber = serialNumber,
        Condition = "Good"
    };

    private static string SerializeSupplies(List<SupplyToCollectDto> supplies) =>
        JsonSerializer.Serialize(supplies);

    private static List<SupplyToCollectDto> DeserializeSupplies(string itemsJson) =>
        JsonSerializer.Deserialize<List<SupplyToCollectDto>>(itemsJson, JsonOpts) ?? [];

    private sealed class StubMissionActivityRepository(List<MissionActivityModel> activities) : IMissionActivityRepository
    {
        public int UpdateCalls { get; private set; }

        public Task<MissionActivityModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(activities.FirstOrDefault(activity => activity.Id == id));

        public Task<IEnumerable<MissionActivityModel>> GetByMissionIdAsync(int missionId, CancellationToken cancellationToken = default)
            => Task.FromResult<IEnumerable<MissionActivityModel>>(activities.Where(activity => activity.MissionId == missionId));

        public Task<IEnumerable<MissionActivityModel>> GetBySosRequestIdsAsync(IEnumerable<int> sosRequestIds, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<int> AddAsync(MissionActivityModel activity, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task UpdateAsync(MissionActivityModel activity, CancellationToken cancellationToken = default)
        {
            UpdateCalls++;
            var storedActivity = activities.FirstOrDefault(item => item.Id == activity.Id);
            if (storedActivity is not null && !ReferenceEquals(storedActivity, activity))
            {
                storedActivity.Items = activity.Items;
                storedActivity.Description = activity.Description;
            }

            return Task.CompletedTask;
        }

        public Task UpdateStatusAsync(int activityId, MissionActivityStatus status, Guid decisionBy, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task AssignTeamAsync(int activityId, int missionTeamId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task ResetAssignmentsToPlannedAsync(IEnumerable<int> activityIds, Guid decisionBy, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task DeleteAsync(int id, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }
}
