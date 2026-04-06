using Microsoft.EntityFrameworkCore;
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
        var entity = await _unitOfWork.Set<InventoryStockThresholdConfig>()
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
        var entities = await _unitOfWork.Set<InventoryStockThresholdConfig>()
            .AsNoTracking()
            .Where(x => x.IsActive
                     && x.DepotId == depotId
                     && (x.ScopeType == "DEPOT" || x.ScopeType == "DEPOT_CATEGORY" || x.ScopeType == "DEPOT_ITEM"))
            .ToListAsync(cancellationToken);

        return entities.Select(Map).ToList();
    }

    public Task<bool> CategoryExistsAsync(int categoryId, CancellationToken cancellationToken = default)
        => _unitOfWork.Set<Category>().AnyAsync(x => x.Id == categoryId, cancellationToken);

    public Task<bool> ItemModelExistsAsync(int itemModelId, CancellationToken cancellationToken = default)
        => _unitOfWork.Set<ItemModel>().AnyAsync(x => x.Id == itemModelId, cancellationToken);

    public async Task<StockThresholdConfigDto> UpsertAsync(
        StockThresholdScopeType scopeType,
        int depotId,
        int? categoryId,
        int? itemModelId,
        decimal? dangerRatio,
        decimal? warningRatio,
        int? minimumThreshold,
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
                MinimumThreshold = minimumThreshold,
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
            entity.MinimumThreshold = minimumThreshold;
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
            MinimumThreshold = x.MinimumThreshold,
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
