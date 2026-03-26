using Microsoft.EntityFrameworkCore;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.UseCases.Logistics.Thresholds;
using RESQ.Domain.Enum.Logistics;
using RESQ.Infrastructure.Entities.Logistics;

namespace RESQ.Infrastructure.Persistence.Logistics;

public class StockThresholdConfigRepository(IUnitOfWork unitOfWork) : IStockThresholdConfigRepository
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<StockThresholdConfigDto?> GetActiveGlobalAsync(CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<InventoryStockThresholdConfig>().AsQueryable()
            .AsNoTracking()
            .Where(x => x.IsActive && x.ScopeType == "GLOBAL")
            .OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return entity == null ? null : Map(entity);
    }

    public async Task<List<StockThresholdConfigDto>> GetActiveDepotScopedConfigsAsync(
        int depotId,
        CancellationToken cancellationToken = default)
    {
        var entities = await _unitOfWork.GetRepository<InventoryStockThresholdConfig>().AsQueryable()
            .AsNoTracking()
            .Where(x => x.IsActive
                     && x.DepotId == depotId
                     && (x.ScopeType == "DEPOT" || x.ScopeType == "DEPOT_CATEGORY" || x.ScopeType == "DEPOT_ITEM"))
            .ToListAsync(cancellationToken);

        return entities.Select(Map).ToList();
    }

    public Task<bool> CategoryExistsAsync(int categoryId, CancellationToken cancellationToken = default)
        => _unitOfWork.GetRepository<Category>().AsQueryable().AnyAsync(x => x.Id == categoryId, cancellationToken);

    public Task<bool> ItemModelExistsAsync(int itemModelId, CancellationToken cancellationToken = default)
        => _unitOfWork.GetRepository<ItemModel>().AsQueryable().AnyAsync(x => x.Id == itemModelId, cancellationToken);

    public async Task<StockThresholdConfigDto> UpsertAsync(
        StockThresholdScopeType scopeType,
        int depotId,
        int? categoryId,
        int? itemModelId,
        decimal dangerRatio,
        decimal warningRatio,
        Guid changedBy,
        uint? expectedRowVersion,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        var scopeValue = ToScopeValue(scopeType);
        var now = DateTime.UtcNow;

        var entity = await FindActiveByScopeAsync(scopeValue, depotId, categoryId, itemModelId, tracked: true, cancellationToken);

        if (entity == null)
        {
            entity = new InventoryStockThresholdConfig
            {
                ScopeType = scopeValue,
                DepotId = scopeType == StockThresholdScopeType.Global ? null : depotId,
                CategoryId = categoryId,
                ItemModelId = itemModelId,
                DangerRatio = dangerRatio,
                WarningRatio = warningRatio,
                UpdatedBy = changedBy,
                UpdatedAt = now,
                IsActive = true,
                RowVersion = 1
            };
            await _unitOfWork.GetRepository<InventoryStockThresholdConfig>().AddAsync(entity);
        }
        else
        {
            if (expectedRowVersion.HasValue && entity.RowVersion != expectedRowVersion.Value)
                throw new ConflictException("Cấu hình đã được cập nhật bởi người khác. Vui lòng tải lại và thử lại.");

            entity.DangerRatio = dangerRatio;
            entity.WarningRatio = warningRatio;
            entity.UpdatedBy = changedBy;
            entity.UpdatedAt = now;
            entity.RowVersion += 1;
        }

        await _unitOfWork.SaveAsync();
        return Map(entity);
    }

    public async Task<StockThresholdConfigDto?> ResetAsync(
        StockThresholdScopeType scopeType,
        int depotId,
        int? categoryId,
        int? itemModelId,
        Guid changedBy,
        uint? expectedRowVersion,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        var scopeValue = ToScopeValue(scopeType);

        var entity = await FindActiveByScopeAsync(scopeValue, depotId, categoryId, itemModelId, tracked: false, cancellationToken);
        if (entity == null)
            return null;

        if (expectedRowVersion.HasValue && entity.RowVersion != expectedRowVersion.Value)
            throw new ConflictException("Cấu hình đã được cập nhật bởi người khác. Vui lòng tải lại và thử lại.");

        var dto = Map(entity);
        await _unitOfWork.GetRepository<InventoryStockThresholdConfig>().DeleteAsyncById(entity.Id);
        await _unitOfWork.SaveAsync();
        return dto;
    }

    public async Task<PagedResult<StockThresholdConfigHistoryDto>> GetHistoryPagedAsync(
        int depotId,
        StockThresholdScopeType? scopeType,
        int? categoryId,
        int? itemModelId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _unitOfWork.GetRepository<InventoryStockThresholdConfigHistory>().AsQueryable()
            .AsNoTracking()
            .Where(x => x.DepotId == depotId);

        if (scopeType.HasValue)
        {
            var scopeValue = ToScopeValue(scopeType.Value);
            query = query.Where(x => x.ScopeType == scopeValue);
        }

        if (categoryId.HasValue)
            query = query.Where(x => x.CategoryId == categoryId.Value);

        if (itemModelId.HasValue)
            query = query.Where(x => x.ItemModelId == itemModelId.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.ChangedAt)
            .ThenByDescending(x => x.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new StockThresholdConfigHistoryDto
            {
                Id = x.Id,
                ConfigId = x.ConfigId,
                ScopeType = ParseScope(x.ScopeType),
                DepotId = x.DepotId ?? 0,
                CategoryId = x.CategoryId,
                ItemModelId = x.ItemModelId,
                OldDangerRatio = x.OldDangerRatio,
                OldWarningRatio = x.OldWarningRatio,
                NewDangerRatio = x.NewDangerRatio,
                NewWarningRatio = x.NewWarningRatio,
                ChangedBy = x.ChangedBy,
                ChangedAt = x.ChangedAt,
                ChangeReason = x.ChangeReason,
                Action = x.Action
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<StockThresholdConfigHistoryDto>(items, totalCount, pageNumber, pageSize);
    }

    public async Task<PagedResult<StockThresholdConfigHistoryDto>> GetAdminHistoryPagedAsync(
        int? depotId,
        StockThresholdScopeType? scopeType,
        int? categoryId,
        int? itemModelId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _unitOfWork.GetRepository<InventoryStockThresholdConfigHistory>().AsQueryable()
            .AsNoTracking();

        if (depotId.HasValue)
            query = query.Where(x => x.DepotId == depotId.Value);

        if (scopeType.HasValue)
        {
            var scopeValue = ToScopeValue(scopeType.Value);
            query = query.Where(x => x.ScopeType == scopeValue);
        }

        if (categoryId.HasValue)
            query = query.Where(x => x.CategoryId == categoryId.Value);

        if (itemModelId.HasValue)
            query = query.Where(x => x.ItemModelId == itemModelId.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.ChangedAt)
            .ThenByDescending(x => x.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new StockThresholdConfigHistoryDto
            {
                Id = x.Id,
                ConfigId = x.ConfigId,
                ScopeType = ParseScope(x.ScopeType),
                DepotId = x.DepotId ?? 0,
                CategoryId = x.CategoryId,
                ItemModelId = x.ItemModelId,
                OldDangerRatio = x.OldDangerRatio,
                OldWarningRatio = x.OldWarningRatio,
                NewDangerRatio = x.NewDangerRatio,
                NewWarningRatio = x.NewWarningRatio,
                ChangedBy = x.ChangedBy,
                ChangedAt = x.ChangedAt,
                ChangeReason = x.ChangeReason,
                Action = x.Action
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<StockThresholdConfigHistoryDto>(items, totalCount, pageNumber, pageSize);
    }

    public async Task<StockThresholdConfigDto> RestoreAsync(
        int configId,
        int? managerDepotId,
        Guid changedBy,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.GetRepository<InventoryStockThresholdConfig>();
        var histRepo = _unitOfWork.GetRepository<InventoryStockThresholdConfigHistory>();
        var now = DateTime.UtcNow;

        // 1. Tìm cấu hình cần khôi phục (không lọc is_active để có thể tìm cả inactive)
        var target = await repo.AsQueryable(tracked: true)
            .FirstOrDefaultAsync(x => x.Id == configId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy cấu hình ngưỡng với id={configId}.");

        // 2. Kiểm tra nó đã active chưa
        if (target.IsActive)
            throw new BadRequestException("Cấu hình này đang được kích hoạt. Không cần khôi phục.");

        // 3. Kiểm tra quyền theo role
        if (managerDepotId == null)
        {
            // Admin chỉ restore GLOBAL
            if (target.ScopeType != "GLOBAL")
                throw new ForbiddenException("Quản trị viên chỉ được khôi phục cấu hình toàn hệ thống (GLOBAL).");
        }
        else
        {
            // Manager chỉ restore cấu hình thuộc kho của mình
            if (target.DepotId != managerDepotId)
                throw new ForbiddenException("Bạn không có quyền khôi phục cấu hình của kho khác.");
        }

        // 4. Deactivate cấu hình đang active cùng scope (nếu có)
        var currentActive = await FindActiveByScopeAsync(
            target.ScopeType,
            target.DepotId ?? 0,
            target.CategoryId,
            target.ItemModelId,
            tracked: true,
            cancellationToken);

        // 4a. Deactivate trước — phải SaveAsync riêng để tránh PostgreSQL vi phạm
        //     partial unique index khi EF gom cả 2 UPDATE vào 1 batch.
        if (currentActive != null)
        {
            currentActive.IsActive = false;
            currentActive.UpdatedBy = changedBy;
            currentActive.UpdatedAt = now;
            currentActive.RowVersion += 1;

            await histRepo.AddAsync(new InventoryStockThresholdConfigHistory
            {
                ConfigId = currentActive.Id,
                ScopeType = currentActive.ScopeType,
                DepotId = currentActive.DepotId,
                CategoryId = currentActive.CategoryId,
                ItemModelId = currentActive.ItemModelId,
                OldDangerRatio = currentActive.DangerRatio,
                OldWarningRatio = currentActive.WarningRatio,
                NewDangerRatio = null,
                NewWarningRatio = null,
                ChangedBy = changedBy,
                ChangedAt = now,
                ChangeReason = reason,
                Action = "RESET"
            });

            // Commit deactivation trước khi activate target
            await _unitOfWork.SaveAsync();
        }

        // 5. Kích hoạt lại cấu hình cũ (SaveAsync lần 2)
        target.IsActive = true;
        target.UpdatedBy = changedBy;
        target.UpdatedAt = now;
        target.RowVersion += 1;

        await histRepo.AddAsync(new InventoryStockThresholdConfigHistory
        {
            ConfigId = target.Id,
            ScopeType = target.ScopeType,
            DepotId = target.DepotId,
            CategoryId = target.CategoryId,
            ItemModelId = target.ItemModelId,
            OldDangerRatio = null,
            OldWarningRatio = null,
            NewDangerRatio = target.DangerRatio,
            NewWarningRatio = target.WarningRatio,
            ChangedBy = changedBy,
            ChangedAt = now,
            ChangeReason = reason,
            Action = "RESTORE"
        });

        await _unitOfWork.SaveAsync();
        return Map(target);
    }

    private async Task<InventoryStockThresholdConfig?> FindActiveByScopeAsync(
        string scopeType,
        int depotId,
        int? categoryId,
        int? itemModelId,
        bool tracked,
        CancellationToken cancellationToken)
    {
        var query = _unitOfWork.GetRepository<InventoryStockThresholdConfig>().AsQueryable(tracked)
            .Where(x => x.IsActive && x.ScopeType == scopeType);

        query = scopeType switch
        {
            "GLOBAL" => query.Where(x => x.DepotId == null && x.CategoryId == null && x.ItemModelId == null),
            "DEPOT" => query.Where(x => x.DepotId == depotId && x.CategoryId == null && x.ItemModelId == null),
            "DEPOT_CATEGORY" => query.Where(x => x.DepotId == depotId && x.CategoryId == categoryId && x.ItemModelId == null),
            "DEPOT_ITEM" => query.Where(x => x.DepotId == depotId && x.CategoryId == null && x.ItemModelId == itemModelId),
            _ => throw new ArgumentOutOfRangeException(nameof(scopeType), scopeType, null)
        };

        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    private static StockThresholdConfigDto Map(InventoryStockThresholdConfig x)
        => new()
        {
            Id = x.Id,
            ScopeType = ParseScope(x.ScopeType),
            DepotId = x.DepotId,
            CategoryId = x.CategoryId,
            ItemModelId = x.ItemModelId,
            DangerRatio = x.DangerRatio,
            WarningRatio = x.WarningRatio,
            IsActive = x.IsActive,
            RowVersion = x.RowVersion,
            UpdatedBy = x.UpdatedBy,
            UpdatedAt = x.UpdatedAt
        };

    private static StockThresholdScopeType ParseScope(string scope)
        => scope switch
        {
            "GLOBAL" => StockThresholdScopeType.Global,
            "DEPOT" => StockThresholdScopeType.Depot,
            "DEPOT_CATEGORY" => StockThresholdScopeType.DepotCategory,
            "DEPOT_ITEM" => StockThresholdScopeType.DepotItem,
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, null)
        };

    private static string ToScopeValue(StockThresholdScopeType scope)
        => scope switch
        {
            StockThresholdScopeType.Global => "GLOBAL",
            StockThresholdScopeType.Depot => "DEPOT",
            StockThresholdScopeType.DepotCategory => "DEPOT_CATEGORY",
            StockThresholdScopeType.DepotItem => "DEPOT_ITEM",
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, null)
        };
}
