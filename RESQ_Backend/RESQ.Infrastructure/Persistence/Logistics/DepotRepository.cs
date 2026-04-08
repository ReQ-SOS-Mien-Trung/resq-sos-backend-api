using Microsoft.EntityFrameworkCore;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Domain.Entities.Logistics;
using RESQ.Domain.Enum.Logistics;
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

    public async Task<PagedResult<DepotModel>> GetAllPagedAsync(int pageNumber, int pageSize, IEnumerable<DepotStatus>? statuses = null, string? search = null, CancellationToken cancellationToken = default)
    {
        var repository = _unitOfWork.GetRepository<Depot>();

        // Convert enum values to string for DB comparison
        var statusStrings = statuses?.Select(s => s.ToString()).ToList();
        var searchTerm = search?.Trim().ToLower();

        // UPDATED: Pass OrderBy to ensure consistent pagination (Order by LastUpdated DESC)
        // UPDATED: Included "DepotManagers.User" to fetch manager details
        var pagedEntities = await repository.GetPagedAsync(
            pageNumber, 
            pageSize,
            filter: d =>
                (statusStrings == null || statusStrings.Count == 0 || statusStrings.Contains(d.Status)) &&
                (searchTerm == null ||
                    (d.Name != null && d.Name.ToLower().Contains(searchTerm)) ||
                    d.DepotManagers.Any(dm =>
                        dm.UnassignedAt == null &&
                        dm.User != null &&
                        ((dm.User.LastName != null && dm.User.LastName.ToLower().Contains(searchTerm)) ||
                         (dm.User.FirstName != null && dm.User.FirstName.ToLower().Contains(searchTerm))))),
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
        return await _unitOfWork.Set<Depot>()
            .CountAsync(
                d => d.Id != depotId
                     && (d.Status == "Available" || d.Status == "Full"
                         || d.Status == "UnderMaintenance" || d.Status == "Closing"),
                cancellationToken);
    }

    public async Task<(int AsSourceCount, int AsRequesterCount)> GetNonTerminalSupplyRequestCountsAsync(
        int depotId, CancellationToken cancellationToken = default)
    {
        // Terminal states for source: Completed, Rejected, Cancelled
        // Terminal states for requester: Received (final happy path), Rejected, Cancelled
        var terminalSourceStatuses      = new[] { "Completed", "Rejected", "Cancelled" };
        var terminalRequestingStatuses  = new[] { "Received",  "Rejected", "Cancelled" };

        var repo = _unitOfWork.Set<DepotSupplyRequest>();

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
        var total = await _unitOfWork.Set<SupplyInventory>()
            .Where(inv => inv.DepotId == depotId && (inv.Quantity ?? 0) > 0)
            .SumAsync(inv => (int?)(inv.Quantity ?? 0), cancellationToken);

        return total ?? 0;
    }

    public async Task<(int AvailableCount, int InUseCount)> GetReusableItemCountsAsync(
        int depotId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Set<ReusableItem>();

        var available = await repo
            .CountAsync(ri => ri.DepotId == depotId && ri.Status == "Available", cancellationToken);

        var inUse = await repo
            .CountAsync(ri => ri.DepotId == depotId && ri.Status == "InUse", cancellationToken);

        return (available, inUse);
    }

    public async Task AssignManagerAsync(DepotModel depot, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        // Unassign manager cũ (nếu có) — set UnassignedAt trực tiếp trên entity
        var existingManagers = await _unitOfWork.Set<DepotManager>()
            .Where(dm => dm.DepotId == depot.Id && dm.UnassignedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var old in existingManagers)
            old.UnassignedAt = now;

        // Thêm bản ghi manager mới
        var newManager = depot.CurrentManager!;
        await _unitOfWork.GetRepository<DepotManager>().AddAsync(new DepotManager
        {
            DepotId    = depot.Id,
            UserId     = newManager.UserId,
            AssignedAt = newManager.AssignedAt
        });

        // Cập nhật status + LastUpdatedAt của kho
        var depotEntity = await _unitOfWork.GetRepository<Depot>()
            .GetByPropertyAsync(x => x.Id == depot.Id, tracked: true);

        if (depotEntity != null)
        {
            depotEntity.Status        = depot.Status.ToString();
            depotEntity.LastUpdatedAt = depot.LastUpdatedAt;
            await _unitOfWork.GetRepository<Depot>().UpdateAsync(depotEntity);
        }
    }

    public async Task UnassignManagerAsync(DepotModel depot, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        // Set UnassignedAt cho tất cả bản ghi manager đang active (không có UnassignedAt)
        // Lịch sử vẫn được giữ lại, chỉ cập nhật UnassignedAt
        var activeManagers = await _unitOfWork.Set<DepotManager>()
            .Where(dm => dm.DepotId == depot.Id && dm.UnassignedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var active in activeManagers)
            active.UnassignedAt = now;

        // Cập nhật status kho → PendingAssignment + LastUpdatedAt
        var depotEntity = await _unitOfWork.GetRepository<Depot>()
            .GetByPropertyAsync(x => x.Id == depot.Id, tracked: true);

        if (depotEntity != null)
        {
            depotEntity.Status        = depot.Status.ToString();
            depotEntity.LastUpdatedAt = depot.LastUpdatedAt;
            await _unitOfWork.GetRepository<Depot>().UpdateAsync(depotEntity);
        }
    }

    public async Task DeleteManagerAsync(DepotModel depot, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        // Set UnassignedAt cho bản ghi manager đang active — giữ lịch sử, không xoá dòng
        var activeManagers = await _unitOfWork.Set<DepotManager>()
            .Where(dm => dm.DepotId == depot.Id && dm.UnassignedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var active in activeManagers)
            active.UnassignedAt = now;

        // Cập nhật status kho → PendingAssignment + LastUpdatedAt
        var depotEntity = await _unitOfWork.GetRepository<Depot>()
            .GetByPropertyAsync(x => x.Id == depot.Id, tracked: true);

        if (depotEntity != null)
        {
            depotEntity.Status        = depot.Status.ToString();
            depotEntity.LastUpdatedAt = depot.LastUpdatedAt;
            await _unitOfWork.GetRepository<Depot>().UpdateAsync(depotEntity);
        }
    }

    public async Task<int> GetConsumableInventoryRowCountAsync(int depotId, CancellationToken cancellationToken = default)
    {
        return await _unitOfWork.Set<SupplyInventory>()
            .CountAsync(inv => inv.DepotId == depotId && (inv.Quantity ?? 0) > 0, cancellationToken);
    }

    public async Task<DepotStatus?> GetStatusByIdAsync(int depotId, CancellationToken cancellationToken = default)
    {
        var statusStr = await _unitOfWork.Set<Depot>()
            .Where(d => d.Id == depotId)
            .Select(d => d.Status)
            .FirstOrDefaultAsync(cancellationToken);

        if (statusStr == null) return null;
        return Enum.TryParse<DepotStatus>(statusStr, out var status) ? status : null;
    }

    public async Task<bool> IsManagerActiveElsewhereAsync(
        Guid managerId, int excludeDepotId, CancellationToken cancellationToken = default)
    {
        return await _unitOfWork.Set<DepotManager>()
            .AnyAsync(
                dm => dm.UserId == managerId
                   && dm.UnassignedAt == null
                   && dm.DepotId != excludeDepotId,
                cancellationToken);
    }
}
