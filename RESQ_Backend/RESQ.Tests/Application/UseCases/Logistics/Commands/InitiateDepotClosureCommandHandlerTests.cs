using Microsoft.Extensions.Logging.Abstractions;
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
        var depot = CreateUnavailableDepot();
        var closure = DepotClosureRecord.Create(
            depotId: depot.Id,
            initiatedBy: Guid.NewGuid(),
            closeReason: "close",
            previousStatus: DepotStatus.Unavailable,
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
        var unitOfWork = new TrackingUnitOfWork();

        var handler = new InitiateDepotClosureCommandHandler(
            depotRepository,
            closureRepository,
            fundDrainService,
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
    public async Task Handle_EmptyDepotWithoutExistingClosure_SavesAfterCreatingClosure()
    {
        var depot = CreateUnavailableDepot();
        var depotRepository = new StubDepotRepository
        {
            Depot = depot,
            ActiveDepotCountExcluding = 1,
            DetailedInventory = []
        };
        var closureRepository = new StubDepotClosureRepository
        {
            CreateResultId = 88
        };
        var fundDrainService = new StubDepotFundDrainService();
        var unitOfWork = new TrackingUnitOfWork();

        var handler = new InitiateDepotClosureCommandHandler(
            depotRepository,
            closureRepository,
            fundDrainService,
            unitOfWork,
            NullLogger<InitiateDepotClosureCommandHandler>.Instance);

        var result = await handler.Handle(
            new InitiateDepotClosureCommand(depot.Id, Guid.NewGuid(), "close"),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(88, result.ClosureId);
        Assert.NotNull(closureRepository.CreatedClosure);
        Assert.Equal(1, unitOfWork.ExecuteInTransactionCalls);
        Assert.Equal(1, unitOfWork.SaveCalls);
        Assert.Equal(88, fundDrainService.LastClosureId);
    }

    private static DepotModel CreateUnavailableDepot()
    {
        var depot = new DepotModel
        {
            Id = 6,
            Name = "Depot 6",
            Status = DepotStatus.Unavailable
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

        public Task AssignManagerAsync(DepotModel depot, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task UnassignManagerAsync(DepotModel depot, CancellationToken cancellationToken = default) => throw new NotImplementedException();

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
