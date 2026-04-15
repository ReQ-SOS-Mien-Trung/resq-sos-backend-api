using Microsoft.EntityFrameworkCore;
using RESQ.Application.Common.Constants;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Logistics.Commands.InitiateDepotClosure;
using RESQ.Application.UseCases.Logistics.Queries.GetDepotManagers;
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
            // Khi kho chuyển sang Closed → unassign tất cả manager đang active (UnassignedAt == null)
            // Xử lý tập trung ở đây để cover tất cả các path đóng kho:
            //   - Kho trống → đóng ngay (InitiateDepotClosure)
            //   - Xử lý hàng bên ngoài (ResolveDepotClosure - External)
            //   - Kho đích xác nhận nhận hàng (ReceiveClosureTransfer)
            bool transitioningToClosed = existingEntity.Status != DepotStatus.Closed.ToString()
                                         && depotModel.Status == DepotStatus.Closed;

            DepotMapper.UpdateEntity(existingEntity, depotModel);

            if (transitioningToClosed)
            {
                var now = DateTime.UtcNow;
                foreach (var dm in existingEntity.DepotManagers.Where(dm => dm.UnassignedAt == null))
                    dm.UnassignedAt = now;
            }

            await repository.UpdateAsync(existingEntity);
        }
    }

    public async Task<PagedResult<DepotModel>> GetAllPagedAsync(int pageNumber, int pageSize, IEnumerable<DepotStatus>? statuses = null, string? search = null, CancellationToken cancellationToken = default)
    {
        var query = _unitOfWork.Set<Depot>()
            .Include(d => d.DepotManagers)
                .ThenInclude(dm => dm.User)
            .AsQueryable();

        // Filter by statuses
        var statusStrings = statuses?.Select(s => s.ToString().ToLower()).ToList();
        if (statusStrings != null && statusStrings.Count > 0)
        {
            query = query.Where(d => statusStrings.Contains(d.Status.ToLower()));
        }

        // Filter by search term (depot name or manager name)
        var searchTerm = search?.Trim().ToLower();
        if (searchTerm != null)
        {
            query = query.Where(d =>
                (d.Name != null && d.Name.ToLower().Contains(searchTerm)) ||
                d.DepotManagers.Any(dm =>
                    dm.UnassignedAt == null &&
                    dm.User != null &&
                    ((dm.User.LastName != null && dm.User.LastName.ToLower().Contains(searchTerm)) ||
                     (dm.User.FirstName != null && dm.User.FirstName.ToLower().Contains(searchTerm)))));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var entities = await query
            .OrderByDescending(d => d.LastUpdatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var domainItems = entities
            .Select(DepotMapper.ToDomain)
            .ToList();

        return new PagedResult<DepotModel>(
            domainItems, 
            totalCount, 
            pageNumber, 
            pageSize
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
                x => x.Status == "Available" && x.CurrentUtilization > 0,
                includeProperties: "DepotManagers.User,SupplyInventories.ItemModel"
            );

        return entities.Select(DepotMapper.ToDomain);
    }

    // --- Closure helpers ------------------------------------------------------

    public async Task<int> GetActiveDepotCountExcludingAsync(int depotId, CancellationToken cancellationToken = default)
    {
        return await _unitOfWork.Set<Depot>()
            .CountAsync(
                d => d.Id != depotId && d.Status == "Available",
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

    public async Task<decimal> GetConsumableTransferVolumeAsync(int depotId, CancellationToken cancellationToken = default)
    {
        var total = await _unitOfWork.Set<SupplyInventory>()
            .Where(inv => inv.DepotId == depotId && (inv.Quantity ?? 0) > 0)
            .Join(
                _unitOfWork.Set<ItemModel>(),
                inv => inv.ItemModelId,
                im => im.Id,
                (inv, im) => (decimal)(inv.Quantity ?? 0) * (im.VolumePerUnit ?? 0m)
            )
            .SumAsync(x => (decimal?)x, cancellationToken);

        return total ?? 0m;
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

    public async Task AssignManagerAsync(DepotModel depot, Guid? assignedBy = null, CancellationToken cancellationToken = default)
    {
        // Thêm bản ghi manager mới - không unassign manager cũ, đó là thao tác riêng biệt
        var newManager = depot.CurrentManager!;

        // Guard chống tạo bản ghi active trùng lặp (UserId, DepotId) ở DB level,
        // phòng trường hợp race condition vượt qua domain guard ở in-memory level.
        var alreadyActiveInDb = await _unitOfWork.Set<DepotManager>()
            .AnyAsync(dm => dm.UserId == newManager.UserId
                         && dm.DepotId == depot.Id
                         && dm.UnassignedAt == null,
                      cancellationToken);

        if (!alreadyActiveInDb)
        {
            await _unitOfWork.GetRepository<DepotManager>().AddAsync(new DepotManager
            {
                DepotId    = depot.Id,
                UserId     = newManager.UserId,
                AssignedAt = newManager.AssignedAt,
                AssignedBy = assignedBy
            });
        }

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

    public async Task UnassignManagerAsync(DepotModel depot, Guid? unassignedBy = null, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        // Set UnassignedAt cho tất cả bản ghi manager đang active (không có UnassignedAt)
        // Lịch sử vẫn được giữ lại, chỉ cập nhật UnassignedAt
        // Phải dùng SetTracked để EF Core theo dõi thay đổi và lưu khi SaveChangesAsync được gọi
        var activeManagers = await _unitOfWork.SetTracked<DepotManager>()
            .Where(dm => dm.DepotId == depot.Id && dm.UnassignedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var active in activeManagers)
        {
            active.UnassignedAt = now;
            active.UnassignedBy = unassignedBy;
        }

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

    public async Task UnassignSpecificManagersAsync(
        DepotModel depot,
        IReadOnlyList<Guid> userIds,
        Guid? unassignedBy = null,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var userIdSet = userIds.ToHashSet();

        // Chỉ set UnassignedAt cho các bản ghi active thuộc userId được chỉ định
        var targets = await _unitOfWork.SetTracked<DepotManager>()
            .Where(dm => dm.DepotId == depot.Id
                      && dm.UnassignedAt == null
                      && dm.UserId != null
                      && userIdSet.Contains(dm.UserId!.Value))
            .ToListAsync(cancellationToken);

        foreach (var dm in targets)
        {
            dm.UnassignedAt = now;
            dm.UnassignedBy = unassignedBy;
        }

        // Cập nhật status kho (domain đã tính: còn manager → Available, hết → PendingAssignment)
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

    public async Task<List<ClosureInventoryItemDto>> GetDetailedInventoryForClosureAsync(
        int depotId, CancellationToken cancellationToken = default)
    {
        // Consumable items: supply_inventory with quantity > 0
        var consumables = await _unitOfWork.Set<SupplyInventory>()
            .Where(inv => inv.DepotId == depotId && (inv.Quantity ?? 0) > 0)
            .Include(inv => inv.ItemModel!)
                .ThenInclude(im => im.Category)
            .Select(inv => new ClosureInventoryItemDto
            {
                ItemModelId  = inv.ItemModelId ?? 0,
                ItemName     = inv.ItemModel!.Name ?? "N/A",
                CategoryName = inv.ItemModel!.Category!.Name ?? "N/A",
                ItemType     = "Consumable",
                Unit         = inv.ItemModel!.Unit ?? "N/A",
                Quantity     = inv.Quantity ?? 0,
                TransferableQuantity = inv.Quantity ?? 0,
                BlockedQuantity = 0,
                VolumePerUnit = inv.ItemModel!.VolumePerUnit,
                WeightPerUnit = inv.ItemModel!.WeightPerUnit
            })
            .ToListAsync(cancellationToken);

        // Reusable items: group by item_model_id, tách số lượng có thể chuyển và số lượng đang bị block
        var reusables = await _unitOfWork.Set<ReusableItem>()
            .Where(ri => ri.DepotId == depotId
                      && ri.Status != "Decommissioned")
            .Include(ri => ri.ItemModel!)
                .ThenInclude(im => im.Category)
            .GroupBy(ri => new
            {
                ItemModelId  = ri.ItemModelId ?? 0,
                ItemName     = ri.ItemModel!.Name ?? "N/A",
                CategoryName = ri.ItemModel!.Category!.Name ?? "N/A",
                Unit         = ri.ItemModel!.Unit ?? "N/A"
            })
            .Select(g => new ClosureInventoryItemDto
            {
                ItemModelId  = g.Key.ItemModelId,
                ItemName     = g.Key.ItemName,
                CategoryName = g.Key.CategoryName,
                ItemType     = "Reusable",
                Unit         = g.Key.Unit,
                Quantity     = g.Count(),
                TransferableQuantity = g.Count(x => x.Status == "Available"),
                BlockedQuantity = g.Count(x => x.Status != "Available"),
                VolumePerUnit = g.Max(x => x.ItemModel!.VolumePerUnit),
                WeightPerUnit = g.Max(x => x.ItemModel!.WeightPerUnit)
            })
            .ToListAsync(cancellationToken);

        return [.. consumables, .. reusables];
    }

    public async Task<List<ClosureInventoryLotItemDto>> GetLotDetailedInventoryForClosureAsync(
        int depotId, CancellationToken cancellationToken = default)
    {
        // Consumable items: chia theo từng lô (supply_inventory_lots) có remaining > 0
        // Include TargetGroups để build chuỗi đối tượng sử dụng
        var consumableLots = await _unitOfWork.Set<SupplyInventoryLot>()
            .Where(lot => lot.SupplyInventory.DepotId == depotId && lot.RemainingQuantity > 0)
            .Include(lot => lot.SupplyInventory)
                .ThenInclude(inv => inv.ItemModel!)
                    .ThenInclude(im => im.Category)
            .Include(lot => lot.SupplyInventory)
                .ThenInclude(inv => inv.ItemModel!)
                    .ThenInclude(im => im.TargetGroups)
            .OrderBy(lot => lot.SupplyInventory.ItemModel!.Name)
            .ThenBy(lot => lot.ExpiredDate)
            .ToListAsync(cancellationToken);

        var consumables = consumableLots.Select(lot => new ClosureInventoryLotItemDto
        {
            ItemModelId  = lot.SupplyInventory.ItemModelId ?? 0,
            ItemName     = lot.SupplyInventory.ItemModel?.Name ?? "N/A",
            CategoryName = lot.SupplyInventory.ItemModel?.Category?.Name ?? "N/A",
            TargetGroup  = TargetGroupTranslations.JoinAsVietnamese(
                lot.SupplyInventory.ItemModel?.TargetGroups.Select(tg => tg.Name) ?? []),
            ItemType     = "Consumable",
            Unit         = lot.SupplyInventory.ItemModel?.Unit ?? "N/A",
            LotId        = lot.Id,
            ReceivedDate = lot.ReceivedDate,
            ExpiredDate  = lot.ExpiredDate,
            Quantity     = lot.RemainingQuantity
        }).ToList();

        // Reusable items: nhóm theo item_model (không có lot)
        var reusableItems = await _unitOfWork.Set<ReusableItem>()
            .Where(ri => ri.DepotId == depotId && ri.Status != "Decommissioned")
            .Include(ri => ri.ItemModel!)
                .ThenInclude(im => im.Category)
            .Include(ri => ri.ItemModel!)
                .ThenInclude(im => im.TargetGroups)
            .ToListAsync(cancellationToken);

        var reusables = reusableItems
            .GroupBy(ri => ri.ItemModelId ?? 0)
            .Select(g =>
            {
                var first = g.First();
                return new ClosureInventoryLotItemDto
                {
                    ItemModelId  = g.Key,
                    ItemName     = first.ItemModel?.Name ?? "N/A",
                    CategoryName = first.ItemModel?.Category?.Name ?? "N/A",
                    TargetGroup  = TargetGroupTranslations.JoinAsVietnamese(
                        first.ItemModel?.TargetGroups.Select(tg => tg.Name) ?? []),
                    ItemType     = "Reusable",
                    Unit         = first.ItemModel?.Unit ?? "N/A",
                    LotId        = null,
                    ReceivedDate = null,
                    ExpiredDate  = null,
                    Quantity     = g.Count()
                };
            })
            .OrderBy(x => x.ItemName)
            .ToList();

        return [.. consumables, .. reusables];
    }

    public async Task<List<ManagedDepotDto>> GetManagedDepotsByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // Step 1: Lấy danh sách DepotId distinct mà user đang active (UnassignedAt IS NULL).
        // Dùng Distinct() để loại bỏ trùng lặp phòng trường hợp có nhiều bản ghi active
        // cùng (UserId, DepotId) do race-condition hay reassign cycle.
        var activeDepotIds = await _unitOfWork.Set<DepotManager>()
            .Where(dm => dm.UserId == userId && dm.UnassignedAt == null && dm.DepotId != null)
            .Select(dm => dm.DepotId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (activeDepotIds.Count == 0)
            return [];

        // Step 2: Lấy thông tin kho cho các DepotId đã lọc.
        return await _unitOfWork.Set<Depot>()
            .Where(d => activeDepotIds.Contains(d.Id))
            .Select(d => new ManagedDepotDto
            {
                DepotId   = d.Id,
                DepotName = d.Name ?? string.Empty,
                Status    = d.Status ?? string.Empty,
                Address   = d.Address ?? string.Empty,
                ImageUrl  = d.ImageUrl
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<List<DepotManagerInfoDto>> GetDepotManagersAsync(int depotId, CancellationToken cancellationToken = default)
    {
        return await _unitOfWork.Set<DepotManager>()
            .Where(dm => dm.DepotId == depotId && dm.UnassignedAt == null && dm.UserId != null)
            .Select(dm => new DepotManagerInfoDto
            {
                UserId     = dm.UserId!.Value,
                FullName   = ((dm.User!.LastName ?? "") + " " + (dm.User.FirstName ?? "")).Trim(),
                Email      = dm.User.Email,
                Phone      = dm.User.Phone,
                AssignedAt = dm.AssignedAt
            })
            .ToListAsync(cancellationToken);
    }
}
