using Microsoft.Extensions.Logging.Abstractions;
using RESQ.Application.Common.Constants;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Logistics.Commands.InitiateDepotClosure;
using RESQ.Domain.Entities.Logistics;
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
        var managerDepotAccessService = new StubManagerDepotAccessService();
        var permissionResolver = new StubUserPermissionResolver();
        var unitOfWork = new TrackingUnitOfWork();

        var handler = new InitiateDepotClosureCommandHandler(
            managerDepotAccessService,
            depotRepository,
            closureRepository,
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
        var managerDepotAccessService = new StubManagerDepotAccessService();
        var permissionResolver = new StubUserPermissionResolver();
        var unitOfWork = new TrackingUnitOfWork();

        var handler = new InitiateDepotClosureCommandHandler(
            managerDepotAccessService,
            depotRepository,
            closureRepository,
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
            => Task.CompletedTask;

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
}

