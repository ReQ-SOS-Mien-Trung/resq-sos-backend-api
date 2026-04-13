using Microsoft.Extensions.Logging.Abstractions;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.Repositories.Personnel;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Logistics.Queries.GetDepotInventoryByCategory;
using RESQ.Application.UseCases.Logistics.Queries.GetLowStockItems;
using RESQ.Application.UseCases.Logistics.Queries.SearchWarehousesByItems;
using RESQ.Application.UseCases.Operations.Shared;
using RESQ.Domain.Entities.Emergency;
using RESQ.Domain.Entities.Logistics;
using RESQ.Domain.Entities.Logistics.Models;
using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Entities.Personnel;
using RESQ.Domain.Enum.Identity;
using RESQ.Domain.Enum.Logistics;
using RESQ.Domain.Enum.Operations;
using RESQ.Tests.TestDoubles;

namespace RESQ.Tests.Application.UseCases.Operations.Shared;

public class MissionActivityStatusExecutionServiceTests
{
    [Fact]
    public async Task ApplyAsync_PersistsImageUrl_ForSucceed()
    {
        const string imageUrl = "https://cdn.example.com/succeed.jpg";

        var activityRepository = CreateActivityRepository(new MissionActivityModel
        {
            Id = 1,
            MissionId = 7,
            Status = MissionActivityStatus.OnGoing,
            ActivityType = "RESCUE"
        });
        var unitOfWork = new StubUnitOfWork();
        var service = BuildService(activityRepository, unitOfWork);

        var result = await service.ApplyAsync(7, 1, MissionActivityStatus.Succeed, Guid.NewGuid(), imageUrl, CancellationToken.None);

        Assert.Equal(MissionActivityStatus.Succeed, result.EffectiveStatus);
        Assert.Equal(imageUrl, result.ImageUrl);
        Assert.Equal(imageUrl, activityRepository.Get(1).ImageUrl);

        var update = Assert.Single(activityRepository.UpdateStatusCalls);
        Assert.Equal(imageUrl, update.imageUrl);
        Assert.Equal(1, unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task ApplyAsync_PersistsImageUrl_ForFailed()
    {
        const string imageUrl = "https://cdn.example.com/failed.jpg";

        var activityRepository = CreateActivityRepository(new MissionActivityModel
        {
            Id = 2,
            MissionId = 8,
            Status = MissionActivityStatus.OnGoing,
            ActivityType = "RESCUE"
        });
        var service = BuildService(activityRepository, new StubUnitOfWork());

        var result = await service.ApplyAsync(8, 2, MissionActivityStatus.Failed, Guid.NewGuid(), imageUrl, CancellationToken.None);

        Assert.Equal(MissionActivityStatus.Failed, result.EffectiveStatus);
        Assert.Equal(imageUrl, result.ImageUrl);
        Assert.Equal(imageUrl, activityRepository.Get(2).ImageUrl);
        Assert.Equal(imageUrl, Assert.Single(activityRepository.UpdateStatusCalls).imageUrl);
    }

    [Fact]
    public async Task ApplyAsync_IgnoresImageUrl_ForNonTerminalStatuses()
    {
        const string existingImageUrl = "https://cdn.example.com/old.jpg";

        var activityRepository = CreateActivityRepository(new MissionActivityModel
        {
            Id = 3,
            MissionId = 9,
            Status = MissionActivityStatus.Planned,
            ActivityType = "RESCUE",
            ImageUrl = existingImageUrl
        });
        var service = BuildService(activityRepository, new StubUnitOfWork());

        var result = await service.ApplyAsync(9, 3, MissionActivityStatus.OnGoing, Guid.NewGuid(), "https://cdn.example.com/new.jpg", CancellationToken.None);

        Assert.Equal(MissionActivityStatus.OnGoing, result.EffectiveStatus);
        Assert.Equal(existingImageUrl, result.ImageUrl);
        Assert.Equal(existingImageUrl, activityRepository.Get(3).ImageUrl);
        Assert.Null(Assert.Single(activityRepository.UpdateStatusCalls).imageUrl);
    }

    [Fact]
    public async Task ApplyAsync_PersistsImageUrl_WhenReturnSuppliesInterceptsToPendingConfirmation()
    {
        const string imageUrl = "https://cdn.example.com/return-supplies.jpg";

        var activityRepository = CreateActivityRepository(new MissionActivityModel
        {
            Id = 4,
            MissionId = 10,
            Status = MissionActivityStatus.OnGoing,
            ActivityType = "RETURN_SUPPLIES"
        });
        var service = BuildService(activityRepository, new StubUnitOfWork());

        var result = await service.ApplyAsync(10, 4, MissionActivityStatus.Succeed, Guid.NewGuid(), imageUrl, CancellationToken.None);

        Assert.Equal(MissionActivityStatus.PendingConfirmation, result.EffectiveStatus);
        Assert.Equal(imageUrl, result.ImageUrl);
        Assert.Equal(imageUrl, activityRepository.Get(4).ImageUrl);

        var update = Assert.Single(activityRepository.UpdateStatusCalls);
        Assert.Equal(MissionActivityStatus.PendingConfirmation, update.status);
        Assert.Equal(imageUrl, update.imageUrl);
    }

    [Fact]
    public async Task ApplyAsync_KeepsExistingImage_WhenLaterTerminalTransitionOmitsImageUrl()
    {
        const string existingImageUrl = "https://cdn.example.com/existing-proof.jpg";

        var activityRepository = CreateActivityRepository(new MissionActivityModel
        {
            Id = 5,
            MissionId = 11,
            Status = MissionActivityStatus.PendingConfirmation,
            ActivityType = "RETURN_SUPPLIES",
            ImageUrl = existingImageUrl
        });
        var service = BuildService(activityRepository, new StubUnitOfWork());

        var result = await service.ApplyAsync(11, 5, MissionActivityStatus.Succeed, Guid.NewGuid(), null, CancellationToken.None);

        Assert.Equal(MissionActivityStatus.Succeed, result.EffectiveStatus);
        Assert.Equal(existingImageUrl, result.ImageUrl);
        Assert.Equal(existingImageUrl, activityRepository.Get(5).ImageUrl);
        Assert.Null(Assert.Single(activityRepository.UpdateStatusCalls).imageUrl);
    }

    private static RecordingMissionActivityRepository CreateActivityRepository(MissionActivityModel activity)
    {
        var repository = new RecordingMissionActivityRepository();
        repository.Upsert(activity);
        return repository;
    }

    private static MissionActivityStatusExecutionService BuildService(
        RecordingMissionActivityRepository activityRepository,
        StubUnitOfWork unitOfWork)
        => new(
            activityRepository,
            new NoOpMissionTeamRepository(),
            new NoOpPersonnelQueryRepository(),
            new NoOpDepotInventoryRepository(),
            new NoOpSosRequestRepository(),
            new NoOpSosRequestUpdateRepository(),
            new NoOpTeamIncidentRepository(),
            unitOfWork,
            NullLogger<MissionActivityStatusExecutionService>.Instance);

    private sealed class RecordingMissionActivityRepository : IMissionActivityRepository
    {
        private readonly Dictionary<int, MissionActivityModel> _activities = [];

        public List<(int activityId, MissionActivityStatus status, Guid decisionBy, string? imageUrl)> UpdateStatusCalls { get; } = [];

        public Task<MissionActivityModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_activities.TryGetValue(id, out var activity) ? activity : null);

        public Task<IEnumerable<MissionActivityModel>> GetByMissionIdAsync(int missionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_activities.Values.Where(activity => activity.MissionId == missionId).AsEnumerable());

        public Task<IEnumerable<MissionActivityModel>> GetBySosRequestIdsAsync(IEnumerable<int> sosRequestIds, CancellationToken cancellationToken = default) =>
            Task.FromResult(Enumerable.Empty<MissionActivityModel>());

        public Task<IReadOnlyList<MissionActivityModel>> GetOpenByAssemblyPointAsync(int assemblyPointId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MissionActivityModel>>([]);

        public Task<int> AddAsync(MissionActivityModel activity, CancellationToken cancellationToken = default)
        {
            _activities[activity.Id] = activity;
            return Task.FromResult(activity.Id);
        }

        public Task UpdateAsync(MissionActivityModel activity, CancellationToken cancellationToken = default)
        {
            _activities[activity.Id] = activity;
            return Task.CompletedTask;
        }

        public Task UpdateStatusAsync(int activityId, MissionActivityStatus status, Guid decisionBy, string? imageUrl = null, CancellationToken cancellationToken = default)
        {
            var activity = _activities[activityId];
            activity.Status = status;
            activity.LastDecisionBy = decisionBy;
            if (!string.IsNullOrWhiteSpace(imageUrl))
            {
                activity.ImageUrl = imageUrl.Trim();
            }

            UpdateStatusCalls.Add((activityId, status, decisionBy, imageUrl));
            return Task.CompletedTask;
        }

        public Task AssignTeamAsync(int activityId, int missionTeamId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ResetAssignmentsToPlannedAsync(IEnumerable<int> activityIds, Guid decisionBy, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(int id, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void Upsert(MissionActivityModel activity) => _activities[activity.Id] = activity;
        public MissionActivityModel Get(int id) => _activities[id];
    }

    private sealed class NoOpMissionTeamRepository : IMissionTeamRepository
    {
        public Task<MissionTeamModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult<MissionTeamModel?>(null);
        public Task<IEnumerable<MissionTeamModel>> GetByMissionIdAsync(int missionId, CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<MissionTeamModel>());
        public Task<int> CreateAsync(MissionTeamModel model, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UpdateStatusAsync(int id, string status, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateStatusAsync(int id, string status, string? note, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateCurrentLocationAsync(int id, double latitude, double longitude, string locationSource, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(int id, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IEnumerable<MissionTeamModel>> GetActiveByRescuerTeamIdAsync(int rescuerTeamId, CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<MissionTeamModel>());
        public Task<MissionTeamModel?> GetByMissionAndTeamAsync(int missionId, int rescuerTeamId, CancellationToken cancellationToken = default) => Task.FromResult<MissionTeamModel?>(null);
    }

    private sealed class NoOpPersonnelQueryRepository : IPersonnelQueryRepository
    {
        public Task<PagedResult<FreeRescuerModel>> GetFreeRescuersAsync(int pageNumber, int pageSize, string? firstName = null, string? lastName = null, string? phone = null, string? email = null, RescuerType? rescuerType = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<PagedResult<RescueTeamModel>> GetAllRescueTeamsAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<RescueTeamModel?> GetRescueTeamDetailAsync(int teamId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<RescueTeamModel?> GetActiveRescueTeamByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult<RescueTeamModel?>(null);
        public Task<PagedResult<FreeRescuerModel>> GetRescuersByAssemblyPointAsync(int assemblyPointId, int pageNumber, int pageSize, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<RescueTeamModel>> GetAllAvailableTeamsAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<PagedResult<RescuerModel>> GetRescuersAsync(int pageNumber, int pageSize, bool? hasAssemblyPoint = null, bool? hasTeam = null, RescuerType? rescuerType = null, string? abilitySubgroupCode = null, string? abilityCategoryCode = null, string? search = null, List<string>? assemblyPointCodes = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class NoOpDepotInventoryRepository : IDepotInventoryRepository
    {
        public Task<int?> GetActiveDepotIdByManagerAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<int>> GetActiveDepotIdsByManagerAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<PagedResult<InventoryItemModel>> GetInventoryPagedAsync(int depotId, List<int>? categoryIds, List<ItemType>? itemTypes, List<TargetGroup>? targetGroups, string? itemName, int pageNumber, int pageSize, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<PagedResult<InventoryLotModel>> GetInventoryLotsAsync(int depotId, int itemModelId, int pageNumber, int pageSize, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<DepotCategoryQuantityDto>> GetInventoryByCategoryAsync(int depotId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<(List<AgentInventoryItem> Items, int TotalCount)> SearchForAgentAsync(string categoryKeyword, string? typeKeyword, int page, int pageSize, IReadOnlyCollection<int>? allowedDepotIds = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<(double Latitude, double Longitude)?> GetDepotLocationAsync(int depotId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<(List<WarehouseItemRow> Rows, int TotalItemCount)> SearchWarehousesByItemsAsync(List<int>? itemModelIds, Dictionary<int, int> itemQuantities, bool activeDepotsOnly, int? excludeDepotId, int pageNumber, int pageSize, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<SupplyShortageResult>> CheckSupplyAvailabilityAsync(int depotId, List<(int ItemModelId, string ItemName, int RequestedQuantity)> items, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<MissionSupplyReservationResult> ReserveSuppliesAsync(int depotId, List<(int ItemModelId, int Quantity)> items, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<MissionSupplyPickupExecutionResult> ConsumeReservedSuppliesAsync(int depotId, List<(int ItemModelId, int Quantity)> items, Guid performedBy, int activityId, int missionId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<MissionSupplyReturnExecutionResult> ReceiveMissionReturnAsync(int depotId, int missionId, int activityId, Guid performedBy, List<(int ItemModelId, int Quantity)> consumableItems, List<(int ReusableItemId, string? Condition, string? Note)> reusableItems, List<(int ItemModelId, int Quantity)> legacyReusableQuantities, string? discrepancyNote, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<LowStockRawItemDto>> GetLowStockRawItemsAsync(int? depotId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task ReleaseReservedSuppliesAsync(int depotId, List<(int ItemModelId, int Quantity)> items, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ExportInventoryAsync(int depotId, int itemModelId, int quantity, Guid performedBy, string? note, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task AdjustInventoryAsync(int depotId, int itemModelId, int quantityChange, Guid performedBy, string reason, string? note, DateTime? expiredDate, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<(int ProcessedRows, int? LastInventoryId)> BulkTransferForClosureAsync(int sourceDepotId, int targetDepotId, int closureId, Guid performedBy, int? lastProcessedInventoryId = null, int batchSize = 100, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task TransferClosureItemsAsync(int sourceDepotId, int targetDepotId, int closureId, int transferId, Guid performedBy, IReadOnlyCollection<DepotClosureTransferItemMoveDto> items, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<Guid?> GetActiveManagerUserIdByDepotIdAsync(int depotId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task ZeroOutForClosureAsync(int depotId, int closureId, Guid performedBy, string? note, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> HasActiveInventoryCommitmentsAsync(int depotId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class NoOpSosRequestRepository : ISosRequestRepository
    {
        public Task CreateAsync(SosRequestModel sosRequest, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UpdateAsync(SosRequestModel sosRequest, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IEnumerable<SosRequestModel>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IEnumerable<SosRequestModel>> GetAllAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<PagedResult<SosRequestModel>> GetAllPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<SosRequestModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IEnumerable<SosRequestModel>> GetByClusterIdAsync(int clusterId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UpdateStatusAsync(int id, RESQ.Domain.Enum.Emergency.SosRequestStatus status, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateStatusByClusterIdAsync(int clusterId, RESQ.Domain.Enum.Emergency.SosRequestStatus status, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IEnumerable<SosRequestModel>> GetByCompanionUserIdAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class NoOpSosRequestUpdateRepository : ISosRequestUpdateRepository
    {
        public Task AddVictimUpdateAsync(SosRequestVictimUpdateModel update, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddIncidentRangeAsync(IEnumerable<SosRequestIncidentUpdateModel> updates, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyDictionary<int, IReadOnlyCollection<int>>> GetSosRequestIdsByTeamIncidentIdsAsync(IEnumerable<int> teamIncidentIds, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyDictionary<int, IReadOnlyCollection<int>>> GetTeamIncidentIdsBySosRequestIdsAsync(IEnumerable<int> sosRequestIds, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyDictionary<int, SosRequestVictimUpdateModel>> GetLatestVictimUpdatesBySosRequestIdsAsync(IEnumerable<int> sosRequestIds, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyDictionary<int, IReadOnlyList<SosRequestIncidentUpdateModel>>> GetIncidentHistoryBySosRequestIdsAsync(IEnumerable<int> sosRequestIds, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class NoOpTeamIncidentRepository : ITeamIncidentRepository
    {
        public Task<IEnumerable<TeamIncidentModel>> GetAllAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<TeamIncidentModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IEnumerable<TeamIncidentModel>> GetByMissionIdAsync(int missionId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IEnumerable<TeamIncidentModel>> GetByMissionTeamIdAsync(int missionTeamId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<int> CreateAsync(TeamIncidentModel model, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UpdateStatusAsync(int id, TeamIncidentStatus status, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateSupportSosRequestIdAsync(int id, int? supportSosRequestId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
