using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.Repositories.Personnel;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Logistics.Queries.GetDepotInventoryByCategory;
using RESQ.Application.UseCases.Logistics.Queries.GetLowStockItems;
using RESQ.Application.UseCases.Logistics.Queries.SearchWarehousesByItems;
using RESQ.Application.UseCases.Operations.Commands.CreateMission;
using RESQ.Application.UseCases.Personnel.Queries.GetAssemblyPointById;
using RESQ.Domain.Entities.Emergency;
using RESQ.Domain.Entities.Logistics;
using RESQ.Domain.Entities.Logistics.Models;
using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Entities.Personnel;
using RESQ.Domain.Entities.Personnel.ValueObjects;
using RESQ.Domain.Enum.Emergency;
using RESQ.Domain.Enum.Logistics;
using RESQ.Domain.Enum.Operations;
using RESQ.Domain.Enum.Personnel;
using RESQ.Tests.TestDoubles;

namespace RESQ.Tests.Application.UseCases.Operations.Commands;

public class CreateMissionCommandHandlerTests
{
    private static readonly Guid CoordinatorId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000010");

    [Fact]
    public async Task Handle_ThrowsBadRequest_WhenSupplyReservationFails()
    {
        var missionRepository = new StubMissionRepository();
        var missionActivityRepository = new StubMissionActivityRepository(missionRepository);
        var clusterRepository = new StubSosClusterRepository(new SosClusterModel { Id = 1 });
        var sosRequestRepository = new StubSosRequestRepository();
        var depotInventoryRepository = new StubDepotInventoryRepository
        {
            ReserveException = new InvalidOperationException("Reserved stock is 0 while mission needs 800.")
        };
        var unitOfWork = new TrackingUnitOfWork();

        var handler = BuildHandler(
            missionRepository,
            missionActivityRepository,
            clusterRepository,
            sosRequestRepository,
            depotInventoryRepository,
            new StubItemModelMetadataRepository(),
            unitOfWork);

        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(BuildCommand(CreateCollectActivity(quantity: 800)), CancellationToken.None));

        Assert.Contains("Reserved stock is 0 while mission needs 800.", ex.Message);
        Assert.Equal(1, unitOfWork.TransactionCalls);
        Assert.Single(depotInventoryRepository.ReserveCalls);
        Assert.Equal(1, missionRepository.CreateCalls);
        Assert.Equal(1, sosRequestRepository.UpdateStatusByClusterCalls);
        Assert.Equal(1, clusterRepository.UpdateCalls);
    }

    [Fact]
    public async Task Handle_ReservesOnlyCollectSuppliesActivities()
    {
        var missionRepository = new StubMissionRepository();
        var missionActivityRepository = new StubMissionActivityRepository(missionRepository);
        var clusterRepository = new StubSosClusterRepository(new SosClusterModel { Id = 1 });
        var sosRequestRepository = new StubSosRequestRepository();
        var depotInventoryRepository = new StubDepotInventoryRepository();
        var unitOfWork = new TrackingUnitOfWork();

        var handler = BuildHandler(
            missionRepository,
            missionActivityRepository,
            clusterRepository,
            sosRequestRepository,
            depotInventoryRepository,
            new StubItemModelMetadataRepository(),
            unitOfWork);

        var response = await handler.Handle(
            BuildCommand(
                CreateCollectActivity(quantity: 10),
                CreateDeliverActivity(quantity: 10)),
            CancellationToken.None);

        var reserveCall = Assert.Single(depotInventoryRepository.ReserveCalls);
        var reservedItem = Assert.Single(reserveCall.Items);

        Assert.Equal(1, unitOfWork.TransactionCalls);
        Assert.Equal(101, response.MissionId);
        Assert.Equal(1, reserveCall.DepotId);
        Assert.Equal(2, reservedItem.ItemModelId);
        Assert.Equal(11, reservedItem.Quantity);
        Assert.Single(depotInventoryRepository.AvailabilityChecks);
    }

    [Fact]
    public async Task Handle_ThrowsBadRequest_WhenAssignedTeamMissingReturnAssemblyPoint()
    {
        var missionRepository = new StubMissionRepository();
        var missionActivityRepository = new StubMissionActivityRepository(missionRepository);
        var clusterRepository = new StubSosClusterRepository(new SosClusterModel { Id = 1 });
        var sosRequestRepository = new StubSosRequestRepository();
        var depotInventoryRepository = new StubDepotInventoryRepository();
        var unitOfWork = new TrackingUnitOfWork();

        var handler = BuildHandler(
            missionRepository,
            missionActivityRepository,
            clusterRepository,
            sosRequestRepository,
            depotInventoryRepository,
            new StubItemModelMetadataRepository(),
            unitOfWork);

        var collect = CreateCollectActivity(quantity: 10);
        collect.RescueTeamId = 12;
        var deliver = CreateDeliverActivity(quantity: 10);
        deliver.RescueTeamId = 12;

        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(BuildCommand(collect, deliver), CancellationToken.None));

        Assert.Contains("Thieu RETURN_ASSEMBLY_POINT", ex.Message);
        Assert.Null(missionRepository.CreatedMission);
    }

    [Fact]
    public async Task Handle_PersistsProvidedReturnAssemblyPoint_WithoutAutoAppending()
    {
        var missionRepository = new StubMissionRepository();
        var missionActivityRepository = new StubMissionActivityRepository(missionRepository);
        var clusterRepository = new StubSosClusterRepository(new SosClusterModel { Id = 1 });
        var sosRequestRepository = new StubSosRequestRepository();
        var depotInventoryRepository = new StubDepotInventoryRepository();
        var unitOfWork = new TrackingUnitOfWork();

        var handler = BuildHandler(
            missionRepository,
            missionActivityRepository,
            clusterRepository,
            sosRequestRepository,
            depotInventoryRepository,
            new StubItemModelMetadataRepository(),
            unitOfWork,
            assemblyPointRepository: new StubAssemblyPointRepository(CreateAssemblyPoint(3)));

        var collect = CreateCollectActivity(quantity: 10);
        collect.RescueTeamId = 12;
        var deliver = CreateDeliverActivity(quantity: 10);
        deliver.RescueTeamId = 12;
        var returnAssembly = CreateReturnAssemblyPointActivity(step: 3, assemblyPointId: 3, rescueTeamId: 12);

        var response = await handler.Handle(BuildCommand(collect, deliver, returnAssembly), CancellationToken.None);

        Assert.Equal(3, response.ActivityCount);
        var persistedReturnAssembly = Assert.Single(missionRepository.CreatedMission!.Activities, activity =>
            string.Equals(activity.ActivityType, "RETURN_ASSEMBLY_POINT", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(3, persistedReturnAssembly.Step);
        Assert.Equal(3, persistedReturnAssembly.AssemblyPointId);
        Assert.Null(persistedReturnAssembly.DepotId);
        Assert.Null(persistedReturnAssembly.Items);
        Assert.Null(persistedReturnAssembly.TargetLatitude);
        Assert.Null(persistedReturnAssembly.TargetLongitude);
    }

    [Fact]
    public async Task Handle_ThrowsBadRequest_WhenReturnAssemblyPointDoesNotExist()
    {
        var missionRepository = new StubMissionRepository();
        var missionActivityRepository = new StubMissionActivityRepository(missionRepository);
        var clusterRepository = new StubSosClusterRepository(new SosClusterModel { Id = 1 });
        var sosRequestRepository = new StubSosRequestRepository();
        var depotInventoryRepository = new StubDepotInventoryRepository();
        var unitOfWork = new TrackingUnitOfWork();

        var handler = BuildHandler(
            missionRepository,
            missionActivityRepository,
            clusterRepository,
            sosRequestRepository,
            depotInventoryRepository,
            new StubItemModelMetadataRepository(),
            unitOfWork,
            assemblyPointRepository: new StubAssemblyPointRepository());

        var collect = CreateCollectActivity(quantity: 10);
        collect.RescueTeamId = 12;
        var deliver = CreateDeliverActivity(quantity: 10);
        deliver.RescueTeamId = 12;
        var returnAssembly = CreateReturnAssemblyPointActivity(step: 3, assemblyPointId: 999, rescueTeamId: 12);

        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(BuildCommand(collect, deliver, returnAssembly), CancellationToken.None));

        Assert.Contains("Khong tim thay diem tap ket #999.", ex.Message);
    }

    private static CreateMissionCommandHandler BuildHandler(
        IMissionRepository missionRepository,
        IMissionActivityRepository missionActivityRepository,
        ISosClusterRepository clusterRepository,
        ISosRequestRepository sosRequestRepository,
        IDepotInventoryRepository depotInventoryRepository,
        IItemModelMetadataRepository itemModelMetadataRepository,
        IUnitOfWork unitOfWork,
        IRescueTeamRepository? rescueTeamRepository = null,
        IAssemblyPointRepository? assemblyPointRepository = null)
    {
        return new CreateMissionCommandHandler(
            missionRepository,
            missionActivityRepository,
            clusterRepository,
            sosRequestRepository,
            depotInventoryRepository,
            new StubDepotRepository(),
            itemModelMetadataRepository,
            rescueTeamRepository ?? new StubRescueTeamRepository(),
            assemblyPointRepository ?? new StubAssemblyPointRepository(),
            unitOfWork,
            new RecordingMediator(),
            new StubFirebaseService(),
            NullLogger<CreateMissionCommandHandler>.Instance);
    }

    private static CreateMissionCommand BuildCommand(params CreateActivityItemDto[] activities)
        => new(
            ClusterId: 1,
            MissionType: "Flood Relief",
            PriorityScore: 90,
            StartTime: new DateTime(2026, 4, 10, 8, 0, 0, DateTimeKind.Utc),
            ExpectedEndTime: new DateTime(2026, 4, 10, 12, 0, 0, DateTimeKind.Utc),
            Activities: activities.ToList(),
            CreatedById: CoordinatorId);

    private static CreateActivityItemDto CreateCollectActivity(int quantity) => new()
    {
        Step = 1,
        ActivityType = "COLLECT_SUPPLIES",
        DepotId = 1,
        DepotName = "Depot A",
        SuppliesToCollect =
        [
            new SuggestedSupplyItemDto
            {
                Id = 2,
                Name = "Nuoc tinh khiet",
                Quantity = quantity,
                Unit = "chai"
            }
        ]
    };

    private static CreateActivityItemDto CreateDeliverActivity(int quantity) => new()
    {
        Step = 2,
        ActivityType = "DELIVER_SUPPLIES",
        DepotId = 1,
        DepotName = "Depot A",
        SuppliesToCollect =
        [
            new SuggestedSupplyItemDto
            {
                Id = 2,
                Name = "Nuoc tinh khiet",
                Quantity = quantity,
                Unit = "chai"
            }
        ]
    };

    private static CreateActivityItemDto CreateReturnAssemblyPointActivity(int step, int assemblyPointId, int rescueTeamId) => new()
    {
        Step = step,
        ActivityType = "RETURN_ASSEMBLY_POINT",
        Description = "Quay ve diem tap ket",
        Priority = "Low",
        EstimatedTime = 20,
        AssemblyPointId = assemblyPointId,
        RescueTeamId = rescueTeamId
    };

    private static AssemblyPointModel CreateAssemblyPoint(
        int id,
        AssemblyPointStatus status = AssemblyPointStatus.Active,
        GeoLocation? location = null) => new()
    {
        Id = id,
        Code = $"AP{id}",
        Name = $"Assembly Point {id}",
        Status = status,
        Location = location ?? new GeoLocation(16.46, 107.59)
    };

    private sealed class StubMissionRepository : IMissionRepository
    {
        public MissionModel? CreatedMission { get; private set; }
        public int CreateCalls { get; private set; }

        public Task<int> CreateAsync(MissionModel mission, Guid coordinatorId, CancellationToken cancellationToken = default)
        {
            CreateCalls++;
            mission.Id = 101;

            for (var index = 0; index < mission.Activities.Count; index++)
            {
                mission.Activities[index].Id = 1001 + index;
                mission.Activities[index].MissionId = mission.Id;
            }

            CreatedMission = mission;
            return Task.FromResult(mission.Id);
        }

        public Task<MissionModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(CreatedMission?.Id == id ? CreatedMission : null);

        public Task<IEnumerable<MissionModel>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerable.Empty<MissionModel>());

        public Task<IEnumerable<MissionModel>> GetByClusterIdAsync(int clusterId, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerable.Empty<MissionModel>());

        public Task<IEnumerable<MissionModel>> GetByIdsAsync(IEnumerable<int> missionIds, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerable.Empty<MissionModel>());

        public Task UpdateAsync(MissionModel mission, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpdateStatusAsync(int missionId, MissionStatus status, bool isCompleted, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class StubMissionActivityRepository(StubMissionRepository missionRepository) : IMissionActivityRepository
    {
        public int UpdateCalls { get; private set; }

        public Task<MissionActivityModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(missionRepository.CreatedMission?.Activities.FirstOrDefault(a => a.Id == id));

        public Task<IEnumerable<MissionActivityModel>> GetByMissionIdAsync(int missionId, CancellationToken cancellationToken = default)
            => Task.FromResult<IEnumerable<MissionActivityModel>>(
                missionRepository.CreatedMission?.Activities.Where(a => a.MissionId == missionId).ToList()
                ?? []);

        public Task<IEnumerable<MissionActivityModel>> GetBySosRequestIdsAsync(IEnumerable<int> sosRequestIds, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerable.Empty<MissionActivityModel>());

        public Task<IReadOnlyList<MissionActivityModel>> GetOpenByAssemblyPointAsync(int assemblyPointId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<MissionActivityModel>>([]);

        public Task<int> AddAsync(MissionActivityModel activity, CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task UpdateAsync(MissionActivityModel activity, CancellationToken cancellationToken = default)
        {
            UpdateCalls++;
            return Task.CompletedTask;
        }

        public Task UpdateStatusAsync(int activityId, MissionActivityStatus status, Guid decisionBy, string? imageUrl = null, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task AssignTeamAsync(int activityId, int missionTeamId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task ResetAssignmentsToPlannedAsync(IEnumerable<int> activityIds, Guid decisionBy, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task DeleteAsync(int id, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class StubSosClusterRepository(SosClusterModel? cluster) : ISosClusterRepository
    {
        private readonly SosClusterModel? _cluster = cluster;
        public int UpdateCalls { get; private set; }

        public Task<SosClusterModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(_cluster?.Id == id ? _cluster : null);

        public Task<IEnumerable<SosClusterModel>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerable.Empty<SosClusterModel>());

        public Task<int> CreateAsync(SosClusterModel cluster, CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task UpdateAsync(SosClusterModel cluster, CancellationToken cancellationToken = default)
        {
            UpdateCalls++;
            return Task.CompletedTask;
        }
    }

    private sealed class StubSosRequestRepository : ISosRequestRepository
    {
        public int UpdateStatusByClusterCalls { get; private set; }

        public Task CreateAsync(RESQ.Domain.Entities.Emergency.SosRequestModel sosRequest, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task UpdateAsync(RESQ.Domain.Entities.Emergency.SosRequestModel sosRequest, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IEnumerable<RESQ.Domain.Entities.Emergency.SosRequestModel>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerable.Empty<RESQ.Domain.Entities.Emergency.SosRequestModel>());

        public Task<IEnumerable<RESQ.Domain.Entities.Emergency.SosRequestModel>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerable.Empty<RESQ.Domain.Entities.Emergency.SosRequestModel>());

        public Task<PagedResult<RESQ.Domain.Entities.Emergency.SosRequestModel>> GetAllPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
            => Task.FromResult(new PagedResult<RESQ.Domain.Entities.Emergency.SosRequestModel>([], 0, pageNumber, pageSize));

        public Task<RESQ.Domain.Entities.Emergency.SosRequestModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult<RESQ.Domain.Entities.Emergency.SosRequestModel?>(null);

        public Task<IEnumerable<RESQ.Domain.Entities.Emergency.SosRequestModel>> GetByClusterIdAsync(int clusterId, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerable.Empty<RESQ.Domain.Entities.Emergency.SosRequestModel>());

        public Task UpdateStatusAsync(int id, SosRequestStatus status, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task UpdateStatusByClusterIdAsync(int clusterId, SosRequestStatus status, CancellationToken cancellationToken = default)
        {
            UpdateStatusByClusterCalls++;
            return Task.CompletedTask;
        }

        public Task<IEnumerable<RESQ.Domain.Entities.Emergency.SosRequestModel>> GetByCompanionUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerable.Empty<RESQ.Domain.Entities.Emergency.SosRequestModel>());
    }

        
        
        
        private sealed class StubDepotRepository : RESQ.Application.Repositories.Logistics.IDepotRepository
    {
        public Task<RESQ.Domain.Enum.Logistics.DepotStatus?> GetStatusByIdAsync(int id, CancellationToken token = default) => Task.FromResult<RESQ.Domain.Enum.Logistics.DepotStatus?>(RESQ.Domain.Enum.Logistics.DepotStatus.Available);
        public Task<RESQ.Domain.Entities.Logistics.DepotModel?> GetByIdAsync(int id, CancellationToken token = default) => throw new NotImplementedException();
        public Task<RESQ.Domain.Entities.Logistics.DepotModel?> GetByNameAsync(string name, CancellationToken token = default) => throw new NotImplementedException();
        public Task<int> GetActiveDepotCountExcludingAsync(int id, CancellationToken token = default) => throw new NotImplementedException();
        public Task CreateAsync(RESQ.Domain.Entities.Logistics.DepotModel d, CancellationToken token = default) => throw new NotImplementedException();
        public Task UpdateAsync(RESQ.Domain.Entities.Logistics.DepotModel d, CancellationToken token = default) => Task.CompletedTask;
        public Task AssignManagerAsync(RESQ.Domain.Entities.Logistics.DepotModel depot, Guid newManagerId, Guid? assignedBy = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UnassignManagerAsync(RESQ.Domain.Entities.Logistics.DepotModel depot, Guid? unassignedBy = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UnassignSpecificManagersAsync(RESQ.Domain.Entities.Logistics.DepotModel depot, IReadOnlyList<Guid> userIds, Guid? unassignedBy = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<RESQ.Application.Common.Models.PagedResult<RESQ.Domain.Entities.Logistics.DepotModel>> GetAllPagedAsync(int pageNumber, int pageSize, System.Collections.Generic.IEnumerable<RESQ.Domain.Enum.Logistics.DepotStatus>? statuses = null, string? search = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<System.Collections.Generic.IEnumerable<RESQ.Domain.Entities.Logistics.DepotModel>> GetAllAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<System.Collections.Generic.IEnumerable<RESQ.Domain.Entities.Logistics.DepotModel>> GetAvailableDepotsAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<(int, int)> GetNonTerminalSupplyRequestCountsAsync(int id, CancellationToken token = default) => throw new NotImplementedException();
        public Task<decimal> GetConsumableTransferVolumeAsync(int depotId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<(int AvailableCount, int InUseCount)> GetReusableItemCountsAsync(int depotId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<int> GetConsumableInventoryRowCountAsync(int depotId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> IsManagerActiveElsewhereAsync(Guid managerId, int excludeDepotId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<System.Collections.Generic.List<RESQ.Application.UseCases.Logistics.Commands.InitiateDepotClosure.ClosureInventoryItemDto>> GetDetailedInventoryForClosureAsync(int depotId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<System.Collections.Generic.List<RESQ.Application.UseCases.Logistics.Commands.InitiateDepotClosure.ClosureInventoryLotItemDto>> GetLotDetailedInventoryForClosureAsync(int depotId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<RESQ.Application.Services.ManagedDepotDto>> GetManagedDepotsByUserAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(new List<RESQ.Application.Services.ManagedDepotDto>());
        public Task<List<RESQ.Application.UseCases.Logistics.Queries.GetDepotManagers.DepotManagerInfoDto>> GetDepotManagersAsync(int depotId, CancellationToken cancellationToken = default) => Task.FromResult(new List<RESQ.Application.UseCases.Logistics.Queries.GetDepotManagers.DepotManagerInfoDto>());
    }
    private sealed class StubDepotInventoryRepository : IDepotInventoryRepository
    {
        public List<(int DepotId, List<(int ItemModelId, string ItemName, int RequestedQuantity)> Items)> AvailabilityChecks { get; } = [];
        public List<(int DepotId, List<(int ItemModelId, int Quantity)> Items)> ReserveCalls { get; } = [];
        public InvalidOperationException? ReserveException { get; set; }

        public Task<int?> GetActiveDepotIdByManagerAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult<int?>(null);

        public Task<List<int>> GetActiveDepotIdsByManagerAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<int>());

        public Task<PagedResult<InventoryItemModel>> GetInventoryPagedAsync(int depotId, List<int>? categoryIds, List<ItemType>? itemTypes, List<TargetGroup>? targetGroups, string? itemName, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<PagedResult<InventoryLotModel>> GetInventoryLotsAsync(int depotId, int itemModelId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<List<DepotCategoryQuantityDto>> GetInventoryByCategoryAsync(int depotId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<(List<AgentInventoryItem> Items, int TotalCount)> SearchForAgentAsync(string categoryKeyword, string? typeKeyword, int page, int pageSize, IReadOnlyCollection<int>? allowedDepotIds = null, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<(double Latitude, double Longitude)?> GetDepotLocationAsync(int depotId, CancellationToken cancellationToken = default)
            => Task.FromResult<(double Latitude, double Longitude)?>(null);

        public Task<(List<WarehouseItemRow> Rows, int TotalItemCount)> SearchWarehousesByItemsAsync(List<int>? itemModelIds, Dictionary<int, int> itemQuantities, bool activeDepotsOnly, int? excludeDepotId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<List<SupplyShortageResult>> CheckSupplyAvailabilityAsync(int depotId, List<(int ItemModelId, string ItemName, int RequestedQuantity)> items, CancellationToken cancellationToken = default)
        {
            AvailabilityChecks.Add((depotId, items));
            return Task.FromResult(new List<SupplyShortageResult>());
        }

        public Task<MissionSupplyReservationResult> ReserveSuppliesAsync(int depotId, List<(int ItemModelId, int Quantity)> items, CancellationToken cancellationToken = default)
        {
            ReserveCalls.Add((depotId, items));

            if (ReserveException is not null)
                throw ReserveException;

            return Task.FromResult(new MissionSupplyReservationResult
            {
                Items = items.Select(item => new SupplyExecutionItemDto
                {
                    ItemModelId = item.ItemModelId,
                    Quantity = item.Quantity
                }).ToList()
            });
        }

        public Task<MissionSupplyPickupExecutionResult> ConsumeReservedSuppliesAsync(int depotId, List<(int ItemModelId, int Quantity)> items, Guid performedBy, int activityId, int missionId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<MissionSupplyReturnExecutionResult> ReceiveMissionReturnAsync(int depotId, int missionId, int activityId, Guid performedBy, List<(int ItemModelId, int Quantity)> consumableItems, List<(int ReusableItemId, string? Condition, string? Note)> reusableItems, List<(int ItemModelId, int Quantity)> legacyReusableQuantities, string? discrepancyNote, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<List<LowStockRawItemDto>> GetLowStockRawItemsAsync(int? depotId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task ReleaseReservedSuppliesAsync(int depotId, List<(int ItemModelId, int Quantity)> items, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task ExportInventoryAsync(int depotId, int itemModelId, int quantity, Guid performedBy, string? note, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task AdjustInventoryAsync(int depotId, int itemModelId, int quantityChange, Guid performedBy, string reason, string? note, DateTime? expiredDate, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<(int ProcessedRows, int? LastInventoryId)> BulkTransferForClosureAsync(int sourceDepotId, int targetDepotId, int closureId, Guid performedBy, int? lastProcessedInventoryId = null, int batchSize = 100, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task TransferClosureItemsAsync(int sourceDepotId, int targetDepotId, int closureId, int transferId,
            Guid performedBy, IReadOnlyCollection<DepotClosureTransferItemMoveDto> items,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<Guid?> GetActiveManagerUserIdByDepotIdAsync(int depotId, CancellationToken ct = default)
            => Task.FromResult<Guid?>(null);

        public Task ZeroOutForClosureAsync(int depotId, int closureId, Guid performedBy, string? note, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<bool> HasActiveInventoryCommitmentsAsync(int depotId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
    }

    private sealed class StubItemModelMetadataRepository : IItemModelMetadataRepository
    {
        public Task<List<MetadataDto>> GetAllForMetadataAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new List<MetadataDto>());

        public Task<List<MetadataDto>> GetByCategoryCodeAsync(ItemCategoryCode categoryCode, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<MetadataDto>());

        public Task<List<DonationImportItemInfo>> GetAllForDonationTemplateAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new List<DonationImportItemInfo>());

        public Task<List<DonationImportTargetGroupInfo>> GetAllTargetGroupsForTemplateAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new List<DonationImportTargetGroupInfo>());

        public Task<Dictionary<int, ItemModelRecord>> GetByIdsAsync(IReadOnlyList<int> ids, CancellationToken cancellationToken = default)
        {
            var map = ids.Distinct().ToDictionary(
                id => id,
                id => new ItemModelRecord
                {
                    Id = id,
                    CategoryId = 2,
                    Name = id == 2 ? "Nuoc tinh khiet" : $"Item {id}",
                    Unit = "chai",
                    ItemType = nameof(ItemType.Consumable)
                });

            return Task.FromResult(map);
        }

        public Task<bool> CategoryExistsAsync(int categoryId, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<bool> HasInventoryTransactionsAsync(int itemModelId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<bool> UpdateItemModelAsync(ItemModelRecord model, CancellationToken cancellationToken = default)
            => Task.FromResult(true);
    }

    private sealed class StubRescueTeamRepository(params RescueTeamModel[] teams) : IRescueTeamRepository
    {
        private readonly Dictionary<int, RescueTeamModel> _teams = teams.ToDictionary(team => team.Id);

        public Task<RescueTeamModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(_teams.GetValueOrDefault(id));

        public Task<RescueTeamModel?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
            => Task.FromResult<RescueTeamModel?>(null);

        public Task<PagedResult<RescueTeamModel>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<bool> IsUserInActiveTeamAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<bool> IsLeaderInActiveTeamAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<Guid?> GetTeamLeaderUserIdByMemberAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult<Guid?>(null);

        public Task<bool> SoftRemoveMemberFromActiveTeamAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<bool> HasRequiredAbilityCategoryAsync(Guid userId, string categoryCode, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<string?> GetTopAbilityCategoryAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);

        public Task CreateAsync(RescueTeamModel team, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task UpdateAsync(RescueTeamModel team, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<int> CountActiveTeamsByAssemblyPointAsync(int assemblyPointId, IEnumerable<int> excludeTeamIds, CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<(List<AgentTeamInfo> Teams, int TotalCount)> GetTeamsForAgentAsync(string? abilityKeyword, bool? available, int page, int pageSize, CancellationToken ct = default)
            => throw new NotImplementedException();
    }

    private sealed class StubAssemblyPointRepository(params AssemblyPointModel[] assemblyPoints) : IAssemblyPointRepository
    {
        private readonly Dictionary<int, AssemblyPointModel> _assemblyPoints = assemblyPoints.ToDictionary(assemblyPoint => assemblyPoint.Id);

        public Task CreateAsync(AssemblyPointModel model, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateAsync(AssemblyPointModel model, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(int id, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<AssemblyPointModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult(_assemblyPoints.GetValueOrDefault(id));
        public Task<AssemblyPointModel?> GetByNameAsync(string name, CancellationToken cancellationToken = default) => Task.FromResult<AssemblyPointModel?>(null);
        public Task<AssemblyPointModel?> GetByCodeAsync(string code, CancellationToken cancellationToken = default) => Task.FromResult<AssemblyPointModel?>(null);
        public Task<PagedResult<AssemblyPointModel>> GetAllPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<AssemblyPointModel>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult(_assemblyPoints.Values.ToList());
        public Task<Dictionary<int, List<AssemblyPointTeamDto>>> GetTeamsByAssemblyPointIdsAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<Guid>> GetAssignedRescuerUserIdsAsync(int assemblyPointId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<Guid>> GetTeamlessRescuerUserIdsAsync(int assemblyPointId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> HasActiveTeamAsync(Guid rescuerUserId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UpdateRescuerAssemblyPointAsync(Guid rescuerUserId, int? assemblyPointId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<List<Guid>> BulkUpdateRescuerAssemblyPointAsync(IReadOnlyList<Guid> userIds, int? assemblyPointId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<Guid>> FilterUsersWithoutActiveTeamAsync(IReadOnlyList<Guid> userIds, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class TrackingUnitOfWork : IUnitOfWork
    {
        public int SaveCalls { get; private set; }
        public int TransactionCalls { get; private set; }

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

        public async Task ExecuteInTransactionAsync(Func<Task> action)
        {
            TransactionCalls++;
            await action();
        }
    }

    private sealed class StubFirebaseService : IFirebaseService
    {
        public Task<FirebasePhoneTokenInfo> VerifyIdTokenAsync(string idToken, CancellationToken cancellationToken = default)
            => Task.FromResult(new FirebasePhoneTokenInfo { Uid = "stub" });

        public Task<FirebaseGoogleUserInfo> VerifyGoogleIdTokenAsync(string idToken, CancellationToken cancellationToken = default)
            => Task.FromResult(new FirebaseGoogleUserInfo { Uid = "stub", Email = "stub@example.com" });

        public Task SendNotificationToUserAsync(Guid userId, string title, string body, string type = "general", CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SendNotificationToUserAsync(Guid userId, string title, string body, string type, Dictionary<string, string> data, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SendToTopicAsync(string topic, string title, string body, string type = "general", CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SendToTopicAsync(string topic, string title, string body, Dictionary<string, string> data, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SubscribeToUserTopicAsync(string fcmToken, Guid userId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task UnsubscribeFromUserTopicAsync(string fcmToken, Guid userId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}








