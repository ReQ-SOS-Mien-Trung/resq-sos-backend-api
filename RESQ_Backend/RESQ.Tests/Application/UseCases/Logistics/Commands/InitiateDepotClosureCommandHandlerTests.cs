using Microsoft.Extensions.Logging.Abstractions;
using RESQ.Application.Common.Constants;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Logistics.Commands.InitiateDepotClosure;
using RESQ.Application.UseCases.Logistics.Queries.GetMyDepotReusableUnits;
using RESQ.Domain.Entities.Logistics;
using RESQ.Domain.Entities.Logistics.Models;
using RESQ.Domain.Entities.Logistics.ValueObjects;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Tests.Application.UseCases.Logistics.Commands;

public class InitiateDepotClosureCommandHandlerTests
{
    [Fact]
    public async Task Handle_FinalizingExistingCompletedClosure_SavesTrackedChanges()
    {
        var depot = CreateClosingDepot();
        var closure = DepotClosureRecord.Create(
            depotId: depot.Id,
            initiatedBy: Guid.NewGuid(),
            closeReason: "close",
            previousStatus: DepotStatus.Available,
            snapshotConsumableUnits: 0,
            snapshotReusableUnits: 0,
            totalConsumableRows: 0,
            totalReusableUnits: 0);
        closure.SetGeneratedId(15);
        closure.Complete(DateTime.UtcNow);

        var depotRepository = new StubDepotRepository
        {
            Depot = depot,
            ActiveDepotCountExcluding = 1,
            DetailedInventory = []
        };
        var closureRepository = new StubDepotClosureRepository
        {
            LatestClosure = closure
        };
        var fundDrainService = new StubDepotFundDrainService();
        var transferRepository = new StubDepotClosureTransferRepository();
        var managerDepotAccessService = new StubManagerDepotAccessService();
        var permissionResolver = new StubUserPermissionResolver();
        var unitOfWork = new TrackingUnitOfWork();

        var handler = new InitiateDepotClosureCommandHandler(
            managerDepotAccessService,
            depotRepository,
            new StubDepotInventoryRepository(),
            closureRepository,
            transferRepository,
            fundDrainService,
            permissionResolver,
            unitOfWork,
            NullLogger<InitiateDepotClosureCommandHandler>.Instance);

        var result = await handler.Handle(
            new InitiateDepotClosureCommand(depot.Id, Guid.NewGuid(), "close"),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(closure.Id, result.ClosureId);
        Assert.Equal(1, unitOfWork.ExecuteInTransactionCalls);
        Assert.Equal(1, unitOfWork.SaveCalls);
        Assert.Equal(closure.Id, fundDrainService.LastClosureId);
        Assert.NotNull(depotRepository.UpdatedDepot);
        Assert.Equal(DepotStatus.Closed, depotRepository.UpdatedDepot!.Status);
        Assert.Null(depotRepository.UpdatedDepot.CurrentManager);
        Assert.NotNull(depotRepository.UpdatedDepot.ManagerHistory.Single().UnassignedAt);
    }

    [Fact]
    public async Task Handle_EmptyDepotWithExistingInProgressClosure_CompletesAndSavesChanges()
    {
        var depot = CreateClosingDepot();
        var closure = DepotClosureRecord.Create(
            depotId: depot.Id,
            initiatedBy: Guid.NewGuid(),
            closeReason: "close",
            previousStatus: DepotStatus.Unavailable,
            snapshotConsumableUnits: 0,
            snapshotReusableUnits: 0,
            totalConsumableRows: 0,
            totalReusableUnits: 0);
        closure.SetGeneratedId(88);

        var depotRepository = new StubDepotRepository
        {
            Depot = depot,
            ActiveDepotCountExcluding = 1,
            DetailedInventory = []
        };
        var closureRepository = new StubDepotClosureRepository
        {
            LatestClosure = closure
        };
        var fundDrainService = new StubDepotFundDrainService();
        var transferRepository = new StubDepotClosureTransferRepository();
        var managerDepotAccessService = new StubManagerDepotAccessService();
        var permissionResolver = new StubUserPermissionResolver();
        var unitOfWork = new TrackingUnitOfWork();

        var handler = new InitiateDepotClosureCommandHandler(
            managerDepotAccessService,
            depotRepository,
            new StubDepotInventoryRepository(),
            closureRepository,
            transferRepository,
            fundDrainService,
            permissionResolver,
            unitOfWork,
            NullLogger<InitiateDepotClosureCommandHandler>.Instance);

        var result = await handler.Handle(
            new InitiateDepotClosureCommand(depot.Id, Guid.NewGuid(), "close"),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(88, result.ClosureId);
        Assert.Equal(DepotClosureStatus.Completed, closure.Status);
        Assert.Equal(1, unitOfWork.ExecuteInTransactionCalls);
        Assert.Equal(1, unitOfWork.SaveCalls);
        Assert.Equal(88, fundDrainService.LastClosureId);
    }

    [Fact]
    public async Task Handle_TransferPendingWithoutOpenTransfers_ReopensClosureAndReturnsRemainingItems()
    {
        var depot = CreateClosingDepot();
        var closure = DepotClosureRecord.Create(
            depotId: depot.Id,
            initiatedBy: Guid.NewGuid(),
            closeReason: "close",
            previousStatus: DepotStatus.Unavailable,
            snapshotConsumableUnits: 12,
            snapshotReusableUnits: 0,
            totalConsumableRows: 1,
            totalReusableUnits: 0);
        closure.SetGeneratedId(101);
        closure.SetTransferResolution(targetDepotId: 9);
        closure.MarkTransferPending();

        var depotRepository = new StubDepotRepository
        {
            Depot = depot,
            ActiveDepotCountExcluding = 1,
            DetailedInventory =
            [
                new ClosureInventoryItemDto
                {
                    ItemModelId = 65,
                    ItemName = "Mì gói",
                    ItemType = "Consumable",
                    Quantity = 6,
                    TransferableQuantity = 6
                }
            ]
        };
        var closureRepository = new StubDepotClosureRepository
        {
            LatestClosure = closure
        };
        var transferRepository = new StubDepotClosureTransferRepository
        {
            HasOpenTransfers = false
        };
        var fundDrainService = new StubDepotFundDrainService();
        var managerDepotAccessService = new StubManagerDepotAccessService();
        var permissionResolver = new StubUserPermissionResolver();
        var unitOfWork = new TrackingUnitOfWork();

        var handler = new InitiateDepotClosureCommandHandler(
            managerDepotAccessService,
            depotRepository,
            new StubDepotInventoryRepository(),
            closureRepository,
            transferRepository,
            fundDrainService,
            permissionResolver,
            unitOfWork,
            NullLogger<InitiateDepotClosureCommandHandler>.Instance);

        var result = await handler.Handle(
            new InitiateDepotClosureCommand(depot.Id, Guid.NewGuid(), "close"),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(closure.Id, result.ClosureId);
        Assert.Single(result.RemainingItems);
        Assert.Equal(DepotClosureStatus.InProgress, closure.Status);
        Assert.Null(closure.ResolutionType);
        Assert.Equal(1, closureRepository.UpdateCalls);
        Assert.Equal(1, unitOfWork.SaveCalls);
        Assert.Equal(0, unitOfWork.ExecuteInTransactionCalls);
        Assert.Null(fundDrainService.LastClosureId);
    }

    private static DepotModel CreateClosingDepot()
    {
        var depot = new DepotModel
        {
            Id = 6,
            Name = "Depot 6",
            Status = DepotStatus.Closing
        };

        depot.AddHistory(
        [
            new DepotManagerAssignment(Guid.NewGuid(), DateTime.UtcNow.AddDays(-5))
        ]);

        return depot;
    }

    private sealed class StubDepotRepository : IDepotRepository
    {
        public DepotModel? Depot { get; set; }
        public int ActiveDepotCountExcluding { get; set; }
        public List<ClosureInventoryItemDto> DetailedInventory { get; set; } = [];
        public DepotModel? UpdatedDepot { get; private set; }

        public Task CreateAsync(DepotModel depotModel, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task UpdateAsync(DepotModel depotModel, CancellationToken cancellationToken = default)
        {
            UpdatedDepot = depotModel;
            return Task.CompletedTask;
        }

        public Task AssignManagerAsync(DepotModel depot, Guid newManagerId, Guid? assignedBy = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task UnassignManagerAsync(DepotModel depot, Guid? unassignedBy = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UnassignSpecificManagersAsync(DepotModel depot, IReadOnlyList<Guid> userIds, Guid? unassignedBy = null, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<RESQ.Application.Common.Models.PagedResult<DepotModel>> GetAllPagedAsync(
            int pageNumber,
            int pageSize,
            IEnumerable<DepotStatus>? statuses = null,
            string? search = null,
            CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<IEnumerable<DepotModel>> GetAllAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<IEnumerable<DepotModel>> GetAvailableDepotsAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<DepotModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(Depot);

        public Task<DepotModel?> GetByNameAsync(string name, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<int> GetActiveDepotCountExcludingAsync(int depotId, CancellationToken cancellationToken = default)
            => Task.FromResult(ActiveDepotCountExcluding);

        public Task<(int AsSourceCount, int AsRequesterCount)> GetNonTerminalSupplyRequestCountsAsync(int depotId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<decimal> GetConsumableTransferVolumeAsync(int depotId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<(int AvailableCount, int InUseCount)> GetReusableItemCountsAsync(int depotId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<int> GetConsumableInventoryRowCountAsync(int depotId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<DepotStatus?> GetStatusByIdAsync(int depotId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<(decimal PendingInboundVolume, decimal PendingInboundWeight)> GetPendingInboundLoadAsync(
            int depotId,
            CancellationToken cancellationToken = default)
            => Task.FromResult((0m, 0m));

        public Task<bool> IsManagerActiveElsewhereAsync(Guid managerId, int excludeDepotId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<List<ClosureInventoryItemDto>> GetDetailedInventoryForClosureAsync(int depotId, CancellationToken cancellationToken = default)
            => Task.FromResult(DetailedInventory);

        public Task<List<ClosureInventoryLotItemDto>> GetLotDetailedInventoryForClosureAsync(int depotId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task<List<RESQ.Application.Services.ManagedDepotDto>> GetManagedDepotsByUserAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(new List<RESQ.Application.Services.ManagedDepotDto>());
        public Task<List<RESQ.Application.UseCases.Logistics.Queries.GetDepotManagers.DepotManagerInfoDto>> GetDepotManagersAsync(int depotId, CancellationToken cancellationToken = default) => Task.FromResult(new List<RESQ.Application.UseCases.Logistics.Queries.GetDepotManagers.DepotManagerInfoDto>());
    }

    private sealed class StubDepotClosureRepository : IDepotClosureRepository
    {
        public DepotClosureRecord? LatestClosure { get; set; }
        public DepotClosureRecord? CreatedClosure { get; private set; }
        public int CreateResultId { get; set; } = 1;
        public int UpdateCalls { get; private set; }

        public Task<int> CreateAsync(DepotClosureRecord record, CancellationToken cancellationToken = default)
        {
            CreatedClosure = record;
            return Task.FromResult(CreateResultId);
        }

        public Task<DepotClosureRecord?> GetByIdAsync(int closureId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<DepotClosureRecord?> GetActiveClosureByDepotIdAsync(int depotId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<DepotClosureRecord?> GetLatestClosureByDepotIdAsync(int depotId, CancellationToken cancellationToken = default)
            => Task.FromResult(LatestClosure);

        public Task UpdateAsync(DepotClosureRecord record, CancellationToken cancellationToken = default)
        {
            UpdateCalls++;
            LatestClosure = record;
            return Task.CompletedTask;
        }

        public Task<bool> TryClaimForProcessingAsync(int closureId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task ResetProcessingToInProgressAsync(int closureId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<bool> TryForceClaimFromProcessingAsync(int closureId, int expectedRowVersion, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task UpdateProgressAsync(int closureId, int processedRows, int lastInventoryId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<List<DepotClosureListItem>> GetClosuresByDepotIdAsync(int depotId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<DepotClosureListItem?> GetClosureDetailAsync(int depotId, int closureId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    private sealed class StubDepotClosureTransferRepository : IDepotClosureTransferRepository
    {
        public bool HasOpenTransfers { get; set; }

        public Task<int> CreateAsync(DepotClosureTransferRecord record, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<int> CreateAsync(
            DepotClosureTransferRecord record,
            IReadOnlyCollection<DepotClosureTransferItemRecord> items,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<DepotClosureTransferRecord?> GetByIdAsync(int transferId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<DepotClosureTransferRecord?> GetByClosureIdAsync(int closureId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<List<DepotClosureTransferRecord>> GetAllByClosureIdAsync(int closureId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<DepotClosureTransferRecord?> GetActiveByClosureIdAsync(int closureId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<bool> HasOpenTransfersAsync(int closureId, CancellationToken cancellationToken = default)
            => Task.FromResult(HasOpenTransfers);

        public Task<DepotClosureTransferRecord?> GetActiveIncomingByTargetDepotIdAsync(int targetDepotId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<List<DepotClosureTransferListItem>> GetByRelatedDepotIdAsync(int depotId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<List<DepotClosureTransferItemRecord>> GetItemsByTransferIdAsync(int transferId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task UpdateAsync(DepotClosureTransferRecord record, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    private sealed class StubManagerDepotAccessService : IManagerDepotAccessService
    {
        public Task<int?> ResolveAccessibleDepotIdAsync(Guid userId, int? requestedDepotId, CancellationToken cancellationToken = default)
            => Task.FromResult<int?>(requestedDepotId);

        public Task<List<ManagedDepotDto>> GetManagedDepotsAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<ManagedDepotDto>());

        public Task EnsureDepotAccessAsync(Guid userId, int depotId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class StubUserPermissionResolver : IUserPermissionResolver
    {
        public Task<IReadOnlyCollection<string>> GetEffectivePermissionCodesAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<string>>(new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                PermissionConstants.InventoryGlobalManage
            });
    }

    private sealed class StubDepotFundDrainService : IDepotFundDrainService
    {
        public int? LastClosureId { get; private set; }

        public Task<decimal> DrainAllToSystemFundAsync(
            int depotId,
            int closureId,
            Guid performedBy,
            CancellationToken cancellationToken = default)
        {
            LastClosureId = closureId;
            return Task.FromResult(0m);
        }
    }

    private sealed class TrackingUnitOfWork : IUnitOfWork
    {
        public int SaveCalls { get; private set; }
        public int ExecuteInTransactionCalls { get; private set; }

        public IGenericRepository<T> GetRepository<T>() where T : class => throw new NotImplementedException();

        public IQueryable<T> Set<T>() where T : class => throw new NotImplementedException();

        public IQueryable<T> SetTracked<T>() where T : class => throw new NotImplementedException();

        public int SaveChangesWithTransaction() => throw new NotImplementedException();

        public Task<int> SaveChangesWithTransactionAsync() => throw new NotImplementedException();

        public Task<int> SaveAsync()
        {
            SaveCalls++;
            return Task.FromResult(1);
        }

        public void AttachAsUnchanged<TEntity>(TEntity entity) where TEntity : class
        {
        }

        public void ClearTrackedChanges()
        {
        }

        public async Task ExecuteInTransactionAsync(Func<Task> action)
        {
            ExecuteInTransactionCalls++;
            await action();
        }
    }

    private sealed class StubDepotInventoryRepository : IDepotInventoryRepository
    {
        public Task<int?> GetActiveDepotIdByManagerAsync(Guid uid, CancellationToken ct = default) => Task.FromResult<int?>(null);
        public Task<List<int>> GetActiveDepotIdsByManagerAsync(Guid uid, CancellationToken ct = default) => Task.FromResult(new List<int>());
        public Task<PagedResult<RESQ.Domain.Entities.Logistics.InventoryItemModel>> GetInventoryPagedAsync(int d, List<int>? c, List<RESQ.Domain.Enum.Logistics.ItemType>? it, List<RESQ.Domain.Enum.Logistics.TargetGroup>? tg, string? n, int pn, int ps, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<PagedResult<InventoryLotModel>> GetInventoryLotsAsync(int d, int i, int pn, int ps, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<RESQ.Application.UseCases.Logistics.Queries.GetDepotInventoryByCategory.DepotCategoryQuantityDto>> GetInventoryByCategoryAsync(int d, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<(List<AgentInventoryItem> Items, int TotalCount)> SearchForAgentAsync(string ck, string? tk, int p, int ps, IReadOnlyCollection<int>? adids = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<(double Latitude, double Longitude)?> GetDepotLocationAsync(int d, CancellationToken ct = default) => Task.FromResult<(double, double)?>(null);
        public Task<(List<RESQ.Application.UseCases.Logistics.Queries.SearchWarehousesByItems.WarehouseItemRow> Rows, int TotalItemCount)> SearchWarehousesByItemsAsync(List<int>? ids, Dictionary<int, int> q, bool a, int? e, int pn, int ps, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<SupplyShortageResult>> CheckSupplyAvailabilityAsync(int depotId, List<(int ItemModelId, string ItemName, int RequestedQuantity)> items, CancellationToken ct = default) => Task.FromResult(new List<SupplyShortageResult>());
        public Task<MissionSupplyReservationResult> ReserveSuppliesAsync(int depotId, List<(int ItemModelId, int Quantity)> items, CancellationToken ct = default) => Task.FromResult(new MissionSupplyReservationResult());
        public Task ReleaseReservedSuppliesAsync(int depotId, List<(int ItemModelId, int Quantity)> items, CancellationToken ct = default) => Task.CompletedTask;
        public Task<MissionSupplyPickupExecutionResult> ConsumeReservedSuppliesAsync(int d, List<(int ItemModelId, int Quantity)> i, Guid pb, int aid, int mid, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<MissionSupplyReturnExecutionResult> ReceiveMissionReturnAsync(int d, int mid, int aid, Guid pb, List<(int ItemModelId, int Quantity, DateTime? ExpiredDate)> ci, List<(int ReusableItemId, string? Condition, string? Note)> ri, List<(int ItemModelId, int Quantity)> lrq, string? dn, CancellationToken ct = default) => throw new NotImplementedException();
        public Task DisposeConsumableLotAsync(int depotId, int lotId, int quantity, string reason, string? note, Guid performedBy, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DecommissionReusableItemAsync(int depotId, int reusableItemId, string? note, Guid performedBy, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<List<ExpiringLotModel>> GetExpiringLotsAsync(int depotId, int daysAhead, CancellationToken cancellationToken = default) => Task.FromResult(new List<ExpiringLotModel>());
        public Task<List<RESQ.Application.UseCases.Logistics.Queries.GetLowStockItems.LowStockRawItemDto>> GetLowStockRawItemsAsync(int? d, CancellationToken ct = default) => throw new NotImplementedException();
        public Task ExportInventoryAsync(int d, int i, int q, Guid pb, string? n, CancellationToken ct = default) => throw new NotImplementedException();
        public Task AdjustInventoryAsync(int d, int i, int qc, Guid pb, string r, string? n, DateTime? e, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<(int ProcessedRows, int? LastInventoryId)> BulkTransferForClosureAsync(int s, int t, int c, Guid pb, int? lp = null, int bs = 100, CancellationToken ct = default) => throw new NotImplementedException();
        public Task TransferClosureItemsAsync(int s, int t, int c, int tid, Guid pb, IReadOnlyCollection<RESQ.Application.Repositories.Logistics.DepotClosureTransferItemMoveDto> i, CancellationToken ct = default) => throw new NotImplementedException();
        public Task ReserveForClosureShipmentAsync(int sourceDepotId, int transferId, int closureId, Guid performedBy, IReadOnlyCollection<RESQ.Application.Repositories.Logistics.DepotClosureTransferItemMoveDto> items, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<PagedResult<ReusableUnitDto>> GetReusableUnitsPagedAsync(int depotId, int? itemModelId, string? serialNumber, List<string>? statuses, List<string>? conditions, int pageNumber, int pageSize, CancellationToken cancellationToken = default) => Task.FromResult(new PagedResult<ReusableUnitDto>([], 0, pageNumber, pageSize));
        public Task<Guid?> GetActiveManagerUserIdByDepotIdAsync(int d, CancellationToken ct = default) => Task.FromResult<Guid?>(null);
        public Task ZeroOutForClosureAsync(int d, int c, Guid pb, string? n, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> HasActiveInventoryCommitmentsAsync(int d, CancellationToken ct = default) => Task.FromResult(false);
    }
}

