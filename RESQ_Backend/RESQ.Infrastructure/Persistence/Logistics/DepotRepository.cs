using Microsoft.EntityFrameworkCore;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Domain.Entities.Logistics;
using RESQ.Infrastructure.Entities.Logistics;
using RESQ.Infrastructure.Mappers.Resources;
using RESQ.Infrastructure.Persistence.Context;

namespace RESQ.Infrastructure.Persistence.Logistics;

public class DepotRepository(IUnitOfWork unitOfWork, ResQDbContext dbContext) : IDepotRepository
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task CreateAsync(DepotModel depotModel, CancellationToken cancellationToken = default)
    {
        var depotEntity = DepotMapper.ToEntity(depotModel);
        await _unitOfWork.GetRepository<Depot>().AddAsync(depotEntity);
    }

    public async Task UpdateAsync(DepotModel depotModel, CancellationToken cancellationToken = default)
    {
        var repository = _unitOfWork.GetRepository<Depot>();
        var existingEntity = await repository.GetByPropertyAsync(
            x => x.Id == depotModel.Id, 
            tracked: true, 
            includeProperties: "DepotManagers.User" // Included User to ensure consistency
        );

        if (existingEntity != null)
        {
            DepotMapper.UpdateEntity(existingEntity, depotModel);
            await repository.UpdateAsync(existingEntity);
        }
    }

    public async Task<PagedResult<DepotModel>> GetAllPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var repository = _unitOfWork.GetRepository<Depot>();
        
        // UPDATED: Pass OrderBy to ensure consistent pagination (Order by LastUpdated DESC)
        // UPDATED: Included "DepotManagers.User" to fetch manager details
        var pagedEntities = await repository.GetPagedAsync(
            pageNumber, 
            pageSize,
            filter: null,
            orderBy: q => q.OrderByDescending(d => d.LastUpdatedAt), 
            includeProperties: "DepotManagers.User"
        );

        var domainItems = pagedEntities.Items
            .Select(DepotMapper.ToDomain)
            .ToList();

        return new PagedResult<DepotModel>(
            domainItems, 
            pagedEntities.TotalCount, 
            pagedEntities.PageNumber, 
            pagedEntities.PageSize
        );
    }

    public async Task<IEnumerable<DepotModel>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _unitOfWork.GetRepository<Depot>()
            .GetAllByPropertyAsync(null, includeProperties: "DepotManagers.User");

        return entities.Select(DepotMapper.ToDomain);
    }

    public async Task<DepotModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<Depot>()
            .GetByPropertyAsync(x => x.Id == id, tracked: false, includeProperties: "DepotManagers.User");

        if (entity == null) return null;

        return DepotMapper.ToDomain(entity);
    }

    public async Task<DepotModel?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<Depot>()
            .GetByPropertyAsync(x => x.Name == name, tracked: false, includeProperties: "DepotManagers.User");

        if (entity == null) return null;

        return DepotMapper.ToDomain(entity);
    }

    public async Task<IEnumerable<DepotModel>> GetAvailableDepotsAsync(CancellationToken cancellationToken = default)
    {
        // Lọc kho đang hoạt động (Available) và còn hàng (CurrentUtilization > 0)
        // Kho Closed, PendingAssignment hoặc trống (utilization = 0) bị loại
        // Include DepotSupplyInventories.ReliefItem để lấy thông tin tồn kho chi tiết
        var entities = await _unitOfWork.GetRepository<Depot>()
            .GetAllByPropertyAsync(
                x => (x.Status == "Available" || x.Status == "Full") && x.CurrentUtilization > 0,
                includeProperties: "DepotManagers.User,SupplyInventories.ItemModel"
            );

        return entities.Select(DepotMapper.ToDomain);
    }

    // ─── Closure helpers ──────────────────────────────────────────────────────

    public async Task<int> GetActiveDepotCountExcludingAsync(int depotId, CancellationToken cancellationToken = default)
    {
        return await _unitOfWork.GetRepository<Depot>().AsQueryable()
            .CountAsync(
                d => d.Id != depotId
                     && (d.Status == "Available" || d.Status == "Full" || d.Status == "Closing"),
                cancellationToken);
    }

    public async Task<(int AsSourceCount, int AsRequesterCount)> GetNonTerminalSupplyRequestCountsAsync(
        int depotId, CancellationToken cancellationToken = default)
    {
        // Terminal states for source: Completed, Rejected, Cancelled
        // Terminal states for requester: Received (final happy path), Rejected, Cancelled
        var terminalSourceStatuses      = new[] { "Completed", "Rejected", "Cancelled" };
        var terminalRequestingStatuses  = new[] { "Received",  "Rejected", "Cancelled" };

        var repo = _unitOfWork.GetRepository<DepotSupplyRequest>().AsQueryable();

        var asSource = await repo
            .CountAsync(r => r.SourceDepotId == depotId
                             && !terminalSourceStatuses.Contains(r.SourceStatus),
                        cancellationToken);

        var asRequester = await repo
            .CountAsync(r => r.RequestingDepotId == depotId
                             && !terminalRequestingStatuses.Contains(r.RequestingStatus),
                        cancellationToken);

        return (asSource, asRequester);
    }

    public async Task<int> GetConsumableTransferVolumeAsync(int depotId, CancellationToken cancellationToken = default)
    {
        var total = await _unitOfWork.GetRepository<SupplyInventory>().AsQueryable()
            .Where(inv => inv.DepotId == depotId && (inv.Quantity ?? 0) > 0)
            .SumAsync(inv => (int?)(inv.Quantity ?? 0), cancellationToken);

        return total ?? 0;
    }

    public async Task<(int AvailableCount, int InUseCount)> GetReusableItemCountsAsync(
        int depotId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.GetRepository<ReusableItem>().AsQueryable();

        var available = await repo
            .CountAsync(ri => ri.DepotId == depotId && ri.Status == "Available", cancellationToken);

        var inUse = await repo
            .CountAsync(ri => ri.DepotId == depotId && ri.Status == "InUse", cancellationToken);

        return (available, inUse);
    }

    public async Task<int> GetConsumableInventoryRowCountAsync(int depotId, CancellationToken cancellationToken = default)
    {
        return await _unitOfWork.GetRepository<SupplyInventory>().AsQueryable()
            .CountAsync(inv => inv.DepotId == depotId && (inv.Quantity ?? 0) > 0, cancellationToken);
    }
}
