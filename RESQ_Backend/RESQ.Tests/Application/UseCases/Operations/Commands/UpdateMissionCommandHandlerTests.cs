using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Logistics.Queries.GetDepotInventoryByCategory;
using RESQ.Application.UseCases.Logistics.Queries.GetLowStockItems;
using RESQ.Application.UseCases.Logistics.Queries.SearchWarehousesByItems;
using RESQ.Application.UseCases.Operations.Commands.UpdateMission;
using RESQ.Application.UseCases.Operations.Queries.GetMissionById;
using RESQ.Application.UseCases.Operations.Queries.GetMissions;
using RESQ.Application.UseCases.Operations.Shared;
using RESQ.Domain.Entities.Logistics;
using RESQ.Domain.Entities.Logistics.Models;
using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Enum.Logistics;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Tests.Application.UseCases.Operations.Commands;

public class UpdateMissionCommandHandlerTests
{
    [Fact]
    public async Task Handle_ThrowsNotFound_WhenMissionDoesNotExist()
    {
        var handler = CreateHandler(
            mission: null,
            activities: [],
            response: new MissionDto { Id = 99 },
            out _,
            out _,
            out _,
            out _,
            out _);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(
                new UpdateMissionCommand(99, "Medical", 90.0, DateTime.UtcNow, DateTime.UtcNow.AddHours(1), null, []),
                CancellationToken.None));
    }

    [Fact]
    public async Task Handle_UpdatesMissionFieldsAndReturnsMissionDto_WhenActivitiesAreNotProvided()
    {
        var mission = new MissionModel
        {
            Id = 15,
            MissionType = "Initial",
            PriorityScore = 10,
            Status = MissionStatus.Planned
        };
        var response = new MissionDto { Id = 15, MissionType = "Medical", ActivityCount = 0 };

        var handler = CreateHandler(
            mission,
            [],
            response,
            out var missionRepository,
            out _,
            out _,
            out var unitOfWork,
            out var sender);

        var startTime = new DateTime(2026, 4, 10, 8, 0, 0, DateTimeKind.Unspecified);
        var expectedEndTime = new DateTime(2026, 4, 10, 12, 30, 0, DateTimeKind.Unspecified);

        var result = await handler.Handle(
            new UpdateMissionCommand(15, "Medical", 88.5, startTime, expectedEndTime, null, []),
            CancellationToken.None);

        Assert.True(missionRepository.UpdateWasCalled);
        Assert.Equal(1, unitOfWork.ExecuteInTransactionCalls);
        Assert.Equal(1, unitOfWork.SaveCalls);
        Assert.Equal("Medical", missionRepository.StoredMission!.MissionType);
        Assert.Equal(88.5, missionRepository.StoredMission.PriorityScore);
        Assert.Equal(DateTimeKind.Utc, missionRepository.StoredMission.StartTime!.Value.Kind);
        Assert.Equal(DateTimeKind.Utc, missionRepository.StoredMission.ExpectedEndTime!.Value.Kind);
        Assert.IsType<GetMissionByIdQuery>(sender.LastRequest);
        Assert.Same(response, result);
    }

    [Fact]
    public async Task Handle_UpdatesMissionAndPendingActivitiesInSingleTransaction()
    {
        var mission = new MissionModel
        {
            Id = 25,
            MissionType = "Initial",
            PriorityScore = 10,
            Status = MissionStatus.OnGoing
        };
        var activity1 = new MissionActivityModel
        {
            Id = 10,
            MissionId = 25,
            MissionTeamId = 7,
            Step = 3,
            Description = "Collect supplies",
            Status = MissionActivityStatus.Planned,
            DepotId = 3,
            ActivityType = "COLLECT_SUPPLIES",
            Items = SerializeSupplies([
                new SupplyToCollectDto { ItemId = 100, ItemName = "Water", Quantity = 10, Unit = "bottle", BufferRatio = 0.10, BufferQuantity = 1 }
            ])
        };
        var activity2 = new MissionActivityModel
        {
            Id = 11,
            MissionId = 25,
            MissionTeamId = 7,
            Step = 4,
            Description = "Deliver supplies",
            Status = MissionActivityStatus.Planned,
            ActivityType = "DELIVER_SUPPLIES"
        };
        var response = new MissionDto { Id = 25, MissionType = "Medical", ActivityCount = 2 };

        var handler = CreateHandler(
            mission,
            [activity1, activity2],
            response,
            out var missionRepository,
            out var activityRepository,
            out var inventoryRepository,
            out var unitOfWork,
            out var sender);

        var result = await handler.Handle(
            new UpdateMissionCommand(
                25,
                "Medical",
                88.5,
                new DateTime(2026, 4, 10, 8, 0, 0, DateTimeKind.Unspecified),
                new DateTime(2026, 4, 10, 12, 30, 0, DateTimeKind.Unspecified),
                Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111"),
                [
                    new UpdateMissionActivityPatch(
                        10,
                        4,
                        "Collect extra water",
                        null,
                        null,
                        null,
                        [new SupplyToCollectDto { ItemId = 100, ItemName = "Water", Quantity = 12, Unit = "bottle", BufferRatio = 0.10 }]),
                    new UpdateMissionActivityPatch(
                        11,
                        3,
                        "Deliver first",
                        "Shelter A",
                        10.123,
                        106.456,
                        null)
                ]),
            CancellationToken.None);

        Assert.True(missionRepository.UpdateWasCalled);
        Assert.Equal(25, missionRepository.StoredMission!.Id);
        Assert.Equal("Medical", missionRepository.StoredMission.MissionType);
        Assert.Equal(1, unitOfWork.ExecuteInTransactionCalls);
        Assert.Equal(4, unitOfWork.SaveCalls);
        Assert.Equal(4, activityRepository.GetById(10)!.Step);
        Assert.Equal(3, activityRepository.GetById(11)!.Step);
        Assert.Equal("Collect extra water", activityRepository.GetById(10)!.Description);
        Assert.Equal("Shelter A", activityRepository.GetById(11)!.Target);
        Assert.Equal(10.123, activityRepository.GetById(11)!.TargetLatitude);
        Assert.Equal(106.456, activityRepository.GetById(11)!.TargetLongitude);
        Assert.Equal(Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111"), activityRepository.GetById(10)!.LastDecisionBy);
        Assert.Single(inventoryRepository.CheckCalls);
        Assert.Single(inventoryRepository.ReleaseCalls);
        Assert.Single(inventoryRepository.ReserveCalls);
        Assert.Equal(3, inventoryRepository.CheckCalls[0].DepotId);
        Assert.Equal(3, inventoryRepository.CheckCalls[0].Items[0].RequestedQuantity);
        Assert.Equal(11, inventoryRepository.ReleaseCalls[0].Items[0].Quantity);
        Assert.Equal(14, inventoryRepository.ReserveCalls[0].Items[0].Quantity);
        Assert.IsType<GetMissionByIdQuery>(sender.LastRequest);
        Assert.Same(response, result);
    }

    [Fact]
    public async Task Handle_DoesNotPersistMission_WhenInventoryValidationFailsForPendingActivities()
    {
        var mission = new MissionModel
        {
            Id = 25,
            MissionType = "Initial",
            PriorityScore = 10,
            Status = MissionStatus.OnGoing
        };
        var activity = new MissionActivityModel
        {
            Id = 10,
            MissionId = 25,
            MissionTeamId = 7,
            Step = 3,
            Status = MissionActivityStatus.Planned,
            DepotId = 3,
            ActivityType = "COLLECT_SUPPLIES",
            Items = SerializeSupplies([
                new SupplyToCollectDto { ItemId = 100, ItemName = "Water", Quantity = 5, Unit = "bottle", BufferRatio = 0.10, BufferQuantity = 1 }
            ])
        };

        var handler = CreateHandler(
            mission,
            [activity],
            new MissionDto { Id = 25 },
            out var missionRepository,
            out _,
            out var inventoryRepository,
            out var unitOfWork,
            out _);
        inventoryRepository.ShortagesToReturn =
        [
            new SupplyShortageResult
            {
                ItemModelId = 100,
                ItemName = "Water",
                RequestedQuantity = 10,
                AvailableQuantity = 2,
                NotFound = false
            }
        ];

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(
                new UpdateMissionCommand(
                    25,
                    "Medical",
                    88.5,
                    new DateTime(2026, 4, 10, 8, 0, 0, DateTimeKind.Unspecified),
                    new DateTime(2026, 4, 10, 12, 30, 0, DateTimeKind.Unspecified),
                    Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111"),
                    [
                        new UpdateMissionActivityPatch(
                            10,
                            null,
                            "need more water",
                            null,
                            null,
                            null,
                            [new SupplyToCollectDto { ItemId = 100, ItemName = "Water", Quantity = 14, Unit = "bottle", BufferRatio = 0.10 }])
                    ]),
                CancellationToken.None));

    Assert.Contains("tồn kho", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(missionRepository.UpdateWasCalled);
        Assert.Equal("Initial", missionRepository.StoredMission!.MissionType);
        Assert.Equal(0, unitOfWork.SaveCalls);
        Assert.Equal(1, unitOfWork.ExecuteInTransactionCalls);
        Assert.Single(inventoryRepository.CheckCalls);
    }

    private static UpdateMissionCommandHandler CreateHandler(
        MissionModel? mission,
        IReadOnlyCollection<MissionActivityModel> activities,
        MissionDto response,
        out StubMissionRepository missionRepository,
        out StubMissionActivityRepository activityRepository,
        out StubDepotInventoryRepository inventoryRepository,
        out TrackingUnitOfWork unitOfWork,
        out StubSender sender)
    {
        missionRepository = new StubMissionRepository(mission);
        activityRepository = new StubMissionActivityRepository(activities);
        inventoryRepository = new StubDepotInventoryRepository();
        unitOfWork = new TrackingUnitOfWork();
        sender = new StubSender(response);
        var pendingActivityUpdateService = new MissionPendingActivityUpdateService(
            activityRepository,
            inventoryRepository,
            unitOfWork,
            NullLogger<MissionPendingActivityUpdateService>.Instance);

        return new UpdateMissionCommandHandler(
            missionRepository,
            unitOfWork,
            pendingActivityUpdateService,
            sender,
            NullLogger<UpdateMissionCommandHandler>.Instance);
    }

    private static string SerializeSupplies(List<SupplyToCollectDto> items) => JsonSerializer.Serialize(items);

    private sealed class StubMissionRepository(MissionModel? mission) : IMissionRepository
    {
        public MissionModel? StoredMission { get; private set; } = mission is null ? null : CloneMission(mission);
        public bool UpdateWasCalled { get; private set; }

        public Task<MissionModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(StoredMission is null ? null : CloneMission(StoredMission));

        public Task UpdateAsync(MissionModel mission, CancellationToken cancellationToken = default)
        {
            UpdateWasCalled = true;
            StoredMission = CloneMission(mission);
            return Task.CompletedTask;
        }

        public Task<IEnumerable<MissionModel>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<MissionModel>());
        public Task<IEnumerable<MissionModel>> GetByClusterIdAsync(int clusterId, CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<MissionModel>());
        public Task<IEnumerable<MissionModel>> GetByIdsAsync(IEnumerable<int> missionIds, CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<MissionModel>());
        public Task<int> CreateAsync(MissionModel mission, Guid coordinatorId, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task UpdateStatusAsync(int missionId, MissionStatus status, bool isCompleted, CancellationToken cancellationToken = default) => Task.CompletedTask;

        private static MissionModel CloneMission(MissionModel mission) => new()
        {
            Id = mission.Id,
            ClusterId = mission.ClusterId,
            MissionType = mission.MissionType,
            PriorityScore = mission.PriorityScore,
            Status = mission.Status,
            StartTime = mission.StartTime,
            ExpectedEndTime = mission.ExpectedEndTime,
            CreatedAt = mission.CreatedAt,
            CompletedAt = mission.CompletedAt,
            Activities = mission.Activities.Select(CloneActivity).ToList()
        };
    }

    private sealed class StubMissionActivityRepository : IMissionActivityRepository
    {
        private readonly Dictionary<int, MissionActivityModel> _activities;

        public StubMissionActivityRepository(IEnumerable<MissionActivityModel> activities)
        {
            _activities = activities.ToDictionary(activity => activity.Id, CloneActivity);
        }

        public MissionActivityModel? GetById(int activityId) => _activities.TryGetValue(activityId, out var activity) ? activity : null;

        public Task<MissionActivityModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(_activities.TryGetValue(id, out var activity) ? CloneActivity(activity) : null);

        public Task<IEnumerable<MissionActivityModel>> GetByMissionIdAsync(int missionId, CancellationToken cancellationToken = default)
            => Task.FromResult(_activities.Values.Where(activity => activity.MissionId == missionId).Select(CloneActivity).AsEnumerable());

        public Task<IEnumerable<MissionActivityModel>> GetBySosRequestIdsAsync(IEnumerable<int> sosRequestIds, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerable.Empty<MissionActivityModel>());

        public Task<int> AddAsync(MissionActivityModel activity, CancellationToken cancellationToken = default) => Task.FromResult(0);

        public Task UpdateAsync(MissionActivityModel activity, CancellationToken cancellationToken = default)
        {
            _activities[activity.Id] = CloneActivity(activity);
            return Task.CompletedTask;
        }

        public Task UpdateStatusAsync(int activityId, MissionActivityStatus status, Guid decisionBy, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AssignTeamAsync(int activityId, int missionTeamId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ResetAssignmentsToPlannedAsync(IEnumerable<int> activityIds, Guid decisionBy, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(int id, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class StubDepotInventoryRepository : IDepotInventoryRepository
    {
        public List<SupplyShortageResult> ShortagesToReturn { get; set; } = [];
        public List<(int DepotId, List<(int ItemModelId, string ItemName, int RequestedQuantity)> Items)> CheckCalls { get; } = [];
        public List<(int DepotId, List<(int ItemModelId, int Quantity)> Items)> ReleaseCalls { get; } = [];
        public List<(int DepotId, List<(int ItemModelId, int Quantity)> Items)> ReserveCalls { get; } = [];

        public Task<List<SupplyShortageResult>> CheckSupplyAvailabilityAsync(int depotId, List<(int ItemModelId, string ItemName, int RequestedQuantity)> items, CancellationToken cancellationToken = default)
        {
            CheckCalls.Add((depotId, items));
            return Task.FromResult(ShortagesToReturn);
        }

        public Task ReleaseReservedSuppliesAsync(int depotId, List<(int ItemModelId, int Quantity)> items, CancellationToken cancellationToken = default)
        {
            ReleaseCalls.Add((depotId, items));
            return Task.CompletedTask;
        }

        public Task<MissionSupplyReservationResult> ReserveSuppliesAsync(int depotId, List<(int ItemModelId, int Quantity)> items, CancellationToken cancellationToken = default)
        {
            ReserveCalls.Add((depotId, items));
            return Task.FromResult(new MissionSupplyReservationResult());
        }

        public Task<int?> GetActiveDepotIdByManagerAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<int>> GetActiveDepotIdsByManagerAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<PagedResult<InventoryItemModel>> GetInventoryPagedAsync(int depotId, List<int>? categoryIds, List<ItemType>? itemTypes, List<TargetGroup>? targetGroups, string? itemName, int pageNumber, int pageSize, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<PagedResult<InventoryLotModel>> GetInventoryLotsAsync(int depotId, int itemModelId, int pageNumber, int pageSize, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<DepotCategoryQuantityDto>> GetInventoryByCategoryAsync(int depotId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<(List<AgentInventoryItem> Items, int TotalCount)> SearchForAgentAsync(string categoryKeyword, string? typeKeyword, int page, int pageSize, IReadOnlyCollection<int>? allowedDepotIds = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<(double Latitude, double Longitude)?> GetDepotLocationAsync(int depotId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<(List<WarehouseItemRow> Rows, int TotalItemCount)> SearchWarehousesByItemsAsync(List<int>? itemModelIds, Dictionary<int, int> itemQuantities, bool activeDepotsOnly, int? excludeDepotId, int pageNumber, int pageSize, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<MissionSupplyPickupExecutionResult> ConsumeReservedSuppliesAsync(int depotId, List<(int ItemModelId, int Quantity)> items, Guid performedBy, int activityId, int missionId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<MissionSupplyReturnExecutionResult> ReceiveMissionReturnAsync(int depotId, int missionId, int activityId, Guid performedBy, List<(int ItemModelId, int Quantity)> consumableItems, List<(int ReusableItemId, string? Condition, string? Note)> reusableItems, List<(int ItemModelId, int Quantity)> legacyReusableQuantities, string? discrepancyNote, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<LowStockRawItemDto>> GetLowStockRawItemsAsync(int? depotId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task ExportInventoryAsync(int depotId, int itemModelId, int quantity, Guid performedBy, string? note, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task AdjustInventoryAsync(int depotId, int itemModelId, int quantityChange, Guid performedBy, string reason, string? note, DateTime? expiredDate, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<(int ProcessedRows, int? LastInventoryId)> BulkTransferForClosureAsync(int sourceDepotId, int targetDepotId, int closureId, Guid performedBy, int? lastProcessedInventoryId = null, int batchSize = 100, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<Guid?> GetActiveManagerUserIdByDepotIdAsync(int depotId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task ZeroOutForClosureAsync(int depotId, int closureId, Guid performedBy, string? note, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> HasActiveInventoryCommitmentsAsync(int depotId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class TrackingUnitOfWork : IUnitOfWork
    {
        public int SaveCalls { get; private set; }
        public int ExecuteInTransactionCalls { get; private set; }

        public IGenericRepository<T> GetRepository<T>() where T : class => throw new NotImplementedException();
        public IQueryable<T> Set<T>() where T : class => throw new NotImplementedException();
        public IQueryable<T> SetTracked<T>() where T : class => throw new NotImplementedException();
        public int SaveChangesWithTransaction() => 1;
        public Task<int> SaveChangesWithTransactionAsync() => Task.FromResult(1);

        public Task<int> SaveAsync()
        {
            SaveCalls++;
            return Task.FromResult(1);
        }

        public void AttachAsUnchanged<TEntity>(TEntity entity) where TEntity : class
        {
        }

        public Task ExecuteInTransactionAsync(Func<Task> action)
        {
            ExecuteInTransactionCalls++;
            return action();
        }
    }

    private sealed class StubSender(MissionDto response) : ISender
    {
        private readonly MissionDto _response = response;

        public object? LastRequest { get; private set; }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult<object?>(_response);
        }

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult((TResponse)(object)_response);
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest
        {
            LastRequest = request;
            return Task.CompletedTask;
        }

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    private static MissionActivityModel CloneActivity(MissionActivityModel activity) => new()
    {
        Id = activity.Id,
        MissionId = activity.MissionId,
        Step = activity.Step,
        ActivityType = activity.ActivityType,
        Description = activity.Description,
        Target = activity.Target,
        Items = activity.Items,
        TargetLatitude = activity.TargetLatitude,
        TargetLongitude = activity.TargetLongitude,
        Status = activity.Status,
        MissionTeamId = activity.MissionTeamId,
        Priority = activity.Priority,
        EstimatedTime = activity.EstimatedTime,
        SosRequestId = activity.SosRequestId,
        DepotId = activity.DepotId,
        DepotName = activity.DepotName,
        DepotAddress = activity.DepotAddress,
        AssemblyPointId = activity.AssemblyPointId,
        AssemblyPointName = activity.AssemblyPointName,
        AssemblyPointLatitude = activity.AssemblyPointLatitude,
        AssemblyPointLongitude = activity.AssemblyPointLongitude,
        AssignedAt = activity.AssignedAt,
        CompletedAt = activity.CompletedAt,
        LastDecisionBy = activity.LastDecisionBy,
        CompletedBy = activity.CompletedBy
    };
}
