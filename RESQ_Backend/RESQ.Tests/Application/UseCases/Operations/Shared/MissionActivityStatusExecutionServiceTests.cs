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
using RESQ.Application.UseCases.Personnel.Queries.GetAssemblyEvents;
using RESQ.Application.UseCases.Personnel.Queries.GetCheckedInRescuers;
using RESQ.Application.UseCases.Personnel.Queries.GetMyAssemblyEvents;
using RESQ.Application.UseCases.Personnel.Queries.GetMyUpcomingAssemblyEvents;
using RESQ.Application.UseCases.Operations.Shared;
using RESQ.Domain.Entities.Emergency;
using RESQ.Domain.Entities.Logistics;
using RESQ.Domain.Entities.Logistics.Models;
using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Entities.Personnel;
using RESQ.Domain.Enum.Identity;
using RESQ.Domain.Enum.Logistics;
using RESQ.Domain.Enum.Operations;
using RESQ.Domain.Enum.Personnel;
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

    [Fact]
    public async Task ApplyAsync_ReturnAssemblyPointSucceed_ChecksInMembersAtActivityAssemblyPointAndMovesTeam()
    {
        var memberId = Guid.Parse("bbbbbbbb-1111-1111-1111-111111111111");
        var removedMemberId = Guid.Parse("bbbbbbbb-2222-2222-2222-222222222222");

        var activityRepository = CreateActivityRepository(new MissionActivityModel
        {
            Id = 6,
            MissionId = 12,
            Status = MissionActivityStatus.OnGoing,
            ActivityType = MissionReturnAssemblyPointStepHelper.ReturnAssemblyPointActivityType,
            MissionTeamId = 30,
            AssemblyPointId = 9,
            AssemblyPointLatitude = 10,
            AssemblyPointLongitude = 20
        });

        var missionTeamRepository = new RecordingMissionTeamRepository(new MissionTeamModel
        {
            Id = 30,
            RescuerTeamId = 40,
            Status = MissionTeamExecutionStatus.InProgress.ToString(),
            AssemblyPointId = 1,
            RescueTeamMembers =
            [
                new MissionTeamMemberInfo { UserId = memberId, Status = TeamMemberStatus.Accepted.ToString() },
                new MissionTeamMemberInfo { UserId = removedMemberId, Status = TeamMemberStatus.Removed.ToString() }
            ]
        });

        var rescueTeam = RescueTeamModel.Create("Return Team", RescueTeamType.Mixed, assemblyPointId: 1, managedBy: Guid.NewGuid());
        rescueTeam.SetId(40);
        var rescueTeamRepository = new RecordingRescueTeamRepository(rescueTeam);
        var assemblyEventRepository = new RecordingAssemblyEventRepository();
        assemblyEventRepository.SetActiveEvent(assemblyPointId: 9, eventId: 90);

        var service = BuildService(
            activityRepository,
            new StubUnitOfWork(),
            missionTeamRepository,
            rescueTeamRepository,
            assemblyEventRepository);

        await service.ApplyAsync(12, 6, MissionActivityStatus.Succeed, Guid.NewGuid(), null, CancellationToken.None);

        var checkIn = Assert.Single(assemblyEventRepository.ReturnCheckInCalls);
        Assert.Equal(90, checkIn.EventId);
        Assert.Equal(memberId, checkIn.RescuerId);
        Assert.Equal(9, rescueTeamRepository.UpdatedTeam?.AssemblyPointId);
    }

    [Fact]
    public async Task ApplyAsync_DoesNotReturnCheckIn_ForNonReturnAssemblyActivity()
    {
        var memberId = Guid.Parse("bbbbbbbb-1111-1111-1111-111111111111");
        var activityRepository = CreateActivityRepository(new MissionActivityModel
        {
            Id = 7,
            MissionId = 13,
            Status = MissionActivityStatus.OnGoing,
            ActivityType = "RESCUE",
            MissionTeamId = 30,
            AssemblyPointId = 9
        });
        var assemblyEventRepository = new RecordingAssemblyEventRepository();
        assemblyEventRepository.SetActiveEvent(assemblyPointId: 9, eventId: 90);

        var service = BuildService(
            activityRepository,
            new StubUnitOfWork(),
            new RecordingMissionTeamRepository(new MissionTeamModel
            {
                Id = 30,
                RescuerTeamId = 40,
                RescueTeamMembers =
                [
                    new MissionTeamMemberInfo { UserId = memberId, Status = TeamMemberStatus.Accepted.ToString() }
                ]
            }),
            new RecordingRescueTeamRepository(null),
            assemblyEventRepository);

        await service.ApplyAsync(13, 7, MissionActivityStatus.Succeed, Guid.NewGuid(), null, CancellationToken.None);

        Assert.Empty(assemblyEventRepository.ReturnCheckInCalls);
    }

    private static RecordingMissionActivityRepository CreateActivityRepository(MissionActivityModel activity)
    {
        var repository = new RecordingMissionActivityRepository();
        repository.Upsert(activity);
        return repository;
    }

    private static MissionActivityStatusExecutionService BuildService(
        RecordingMissionActivityRepository activityRepository,
        StubUnitOfWork unitOfWork,
        IMissionTeamRepository? missionTeamRepository = null,
        IRescueTeamRepository? rescueTeamRepository = null,
        IAssemblyEventRepository? assemblyEventRepository = null)
        => new(
            activityRepository,
            missionTeamRepository ?? new RecordingMissionTeamRepository(),
            new NoOpPersonnelQueryRepository(),
            new NoOpDepotInventoryRepository(),
            new NoOpSosRequestRepository(),
            new NoOpSosRequestUpdateRepository(),
            new NoOpTeamIncidentRepository(),
            rescueTeamRepository ?? new RecordingRescueTeamRepository(null),
            unitOfWork,
            NullLogger<MissionActivityStatusExecutionService>.Instance,
            assemblyEventRepository ?? new RecordingAssemblyEventRepository());

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

    private sealed class RecordingMissionTeamRepository(MissionTeamModel? missionTeam = null) : IMissionTeamRepository
    {
        public List<(int Id, string Status)> StatusUpdates { get; } = [];
        public List<(int Id, double Latitude, double Longitude, string Source)> LocationUpdates { get; } = [];

        public Task<MissionTeamModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult(missionTeam?.Id == id ? missionTeam : null);
        public Task<IEnumerable<MissionTeamModel>> GetByMissionIdAsync(int missionId, CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<MissionTeamModel>());
        public Task<int> CreateAsync(MissionTeamModel model, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UpdateStatusAsync(int id, string status, CancellationToken cancellationToken = default)
        {
            StatusUpdates.Add((id, status));
            return Task.CompletedTask;
        }
        public Task UpdateStatusAsync(int id, string status, string? note, CancellationToken cancellationToken = default) => UpdateStatusAsync(id, status, cancellationToken);
        public Task UpdateCurrentLocationAsync(int id, double latitude, double longitude, string locationSource, CancellationToken cancellationToken = default)
        {
            LocationUpdates.Add((id, latitude, longitude, locationSource));
            return Task.CompletedTask;
        }
        public Task DeleteAsync(int id, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IEnumerable<MissionTeamModel>> GetActiveByRescuerTeamIdAsync(int rescuerTeamId, CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<MissionTeamModel>());
        public Task<MissionTeamModel?> GetByMissionAndTeamAsync(int missionId, int rescuerTeamId, CancellationToken cancellationToken = default) => Task.FromResult<MissionTeamModel?>(null);
    }

    private sealed class RecordingRescueTeamRepository(RescueTeamModel? team) : IRescueTeamRepository
    {
        public RescueTeamModel? UpdatedTeam { get; private set; }

        public Task<RescueTeamModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult(team?.Id == id ? team : null);
        public Task<RescueTeamModel?> GetByCodeAsync(string code, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<PagedResult<RescueTeamModel>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> IsUserInActiveTeamAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> IsLeaderInActiveTeamAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<Guid?> GetTeamLeaderUserIdByMemberAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult<Guid?>(null);
        public Task<bool> SoftRemoveMemberFromActiveTeamAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> HasRequiredAbilityCategoryAsync(Guid userId, string categoryCode, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<string?> GetTopAbilityCategoryAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task CreateAsync(RescueTeamModel team, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UpdateAsync(RescueTeamModel team, CancellationToken cancellationToken = default)
        {
            UpdatedTeam = team;
            return Task.CompletedTask;
        }
        public Task<int> CountActiveTeamsByAssemblyPointAsync(int assemblyPointId, IEnumerable<int> excludeTeamIds, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<(List<AgentTeamInfo> Teams, int TotalCount)> GetTeamsForAgentAsync(string? abilityKeyword, bool? available, int page, int pageSize, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class RecordingAssemblyEventRepository : IAssemblyEventRepository
    {
        private readonly Dictionary<int, (int EventId, string Status)> _activeEvents = [];

        public List<(int EventId, Guid RescuerId)> ReturnCheckInCalls { get; } = [];

        public void SetActiveEvent(int assemblyPointId, int eventId)
            => _activeEvents[assemblyPointId] = (eventId, AssemblyEventStatus.Gathering.ToString());

        public Task<(int EventId, string Status)?> GetActiveEventByAssemblyPointAsync(int assemblyPointId, CancellationToken cancellationToken = default)
            => Task.FromResult(_activeEvents.TryGetValue(assemblyPointId, out var value) ? value : ((int EventId, string Status)?)null);

        public Task<bool> ReturnCheckInAsync(int eventId, Guid rescuerId, CancellationToken cancellationToken = default)
        {
            ReturnCheckInCalls.Add((eventId, rescuerId));
            return Task.FromResult(true);
        }

        public Task<int> CreateEventAsync(int assemblyPointId, DateTime assemblyDate, DateTime checkInDeadline, Guid createdBy, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task AssignParticipantsAsync(int eventId, List<Guid> rescuerIds, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> CheckInAsync(int eventId, Guid rescuerId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> CheckOutAsync(int eventId, Guid rescuerId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> CheckOutVoluntaryAsync(int eventId, Guid rescuerId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> IsParticipantCheckedInAsync(int eventId, Guid rescuerId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> HasParticipantCheckedOutAsync(int eventId, Guid rescuerId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<PagedResult<CheckedInRescuerDto>> GetCheckedInRescuersAsync(int eventId, int pageNumber, int pageSize, RescuerType? rescuerType = null, string? abilitySubgroupCode = null, string? abilityCategoryCode = null, string? search = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<PagedResult<AssemblyEventListItemDto>> GetEventsByAssemblyPointAsync(int assemblyPointId, int pageNumber, int pageSize, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UpdateEventStatusAsync(int eventId, string status, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<Guid>> GetParticipantIdsAsync(int eventId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task StartGatheringAsync(int eventId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<(int EventId, int AssemblyPointId, string Status, DateTime AssemblyDate, DateTime? CheckInDeadline)?> GetEventByIdAsync(int eventId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<Guid?> GetEventCreatedByAsync(int eventId, CancellationToken cancellationToken = default) => Task.FromResult<Guid?>(null);
        public Task<bool> MarkParticipantAbsentAsync(int eventId, Guid rescuerId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<PagedResult<MyAssemblyEventDto>> GetAssemblyEventsForRescuerAsync(Guid rescuerId, int pageNumber, int pageSize, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<UpcomingAssemblyEventDto>> GetUpcomingEventsForRescuerAsync(Guid rescuerId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<int>> GetScheduledEventsReadyForGatheringAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<int>> GetGatheringEventsWithExpiredDeadlineAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<int>> GetGatheringEventsExpiredAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task CompleteEventAsync(int eventId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<int> AutoMarkAbsentForEventAsync(int eventId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
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
        public Task<MissionSupplyReturnExecutionResult> ReceiveMissionReturnAsync(int depotId, int missionId, int activityId, Guid performedBy, List<(int ItemModelId, int Quantity, DateTime? ExpiredDate)> consumableItems, List<(int ReusableItemId, string? Condition, string? Note)> reusableItems, List<(int ItemModelId, int Quantity)> legacyReusableQuantities, string? discrepancyNote, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<LowStockRawItemDto>> GetLowStockRawItemsAsync(int? depotId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task ReleaseReservedSuppliesAsync(int depotId, List<(int ItemModelId, int Quantity)> items, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ExportInventoryAsync(int depotId, int itemModelId, int quantity, Guid performedBy, string? note, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task AdjustInventoryAsync(int depotId, int itemModelId, int quantityChange, Guid performedBy, string reason, string? note, DateTime? expiredDate, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<(int ProcessedRows, int? LastInventoryId)> BulkTransferForClosureAsync(int sourceDepotId, int targetDepotId, int closureId, Guid performedBy, int? lastProcessedInventoryId = null, int batchSize = 100, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task TransferClosureItemsAsync(int sourceDepotId, int targetDepotId, int closureId, int transferId, Guid performedBy, IReadOnlyCollection<DepotClosureTransferItemMoveDto> items, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<Guid?> GetActiveManagerUserIdByDepotIdAsync(int depotId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task ZeroOutForClosureAsync(int depotId, int closureId, Guid performedBy, string? note, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> HasActiveInventoryCommitmentsAsync(int depotId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task DisposeConsumableLotAsync(int lotId, int quantity, string reason, string? note, Guid performedBy, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DecommissionReusableItemAsync(int reusableItemId, string? note, Guid performedBy, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<List<ExpiringLotModel>> GetExpiringLotsAsync(int depotId, int daysAhead, CancellationToken cancellationToken = default) => Task.FromResult(new List<ExpiringLotModel>());
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
