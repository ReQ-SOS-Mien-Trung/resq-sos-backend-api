using RESQ.Application.UseCases.Logistics.Thresholds;
using RESQ.Application.Common.Models;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.Repositories.Logistics;

public interface IStockThresholdConfigRepository
{
    Task<StockThresholdConfigDto?> GetActiveGlobalAsync(CancellationToken cancellationToken = default);

    Task<List<StockThresholdConfigDto>> GetActiveDepotScopedConfigsAsync(
        int depotId,
        CancellationToken cancellationToken = default);

    Task<bool> CategoryExistsAsync(int categoryId, CancellationToken cancellationToken = default);
    Task<bool> ItemModelExistsAsync(int itemModelId, CancellationToken cancellationToken = default);

    Task<StockThresholdConfigDto> UpsertAsync(
        StockThresholdScopeType scopeType,
        int depotId,
        int? categoryId,
        int? itemModelId,
        decimal dangerRatio,
        decimal warningRatio,
        Guid changedBy,
        uint? expectedRowVersion,
        string? reason,
        CancellationToken cancellationToken = default);

    Task<StockThresholdConfigDto?> ResetAsync(
        StockThresholdScopeType scopeType,
        int depotId,
        int? categoryId,
        int? itemModelId,
        Guid changedBy,
        uint? expectedRowVersion,
        string? reason,
        CancellationToken cancellationToken = default);

    Task<PagedResult<StockThresholdConfigHistoryDto>> GetHistoryPagedAsync(
        int depotId,
        StockThresholdScopeType? scopeType,
        int? categoryId,
        int? itemModelId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Khôi phục một cấu hình không hoạt động (inactive) thành active.
    /// Tự động deactivate cấu hình đang active cùng scope trước khi kích hoạt lại.
    /// </summary>
    /// <param name="configId">ID của cấu hình cần khôi phục.</param>
    /// <param name="managerDepotId">
    ///   null = admin (chỉ restore được GLOBAL scope).
    ///   int  = manager (chỉ restore được cấu hình thuộc kho của mình).
    /// </param>
    Task<StockThresholdConfigDto> RestoreAsync(
        int configId,
        int? managerDepotId,
        Guid changedBy,
        string? reason,
        CancellationToken cancellationToken = default);

    Task<PagedResult<StockThresholdConfigHistoryDto>> GetAdminHistoryPagedAsync(
        int? depotId,
        StockThresholdScopeType? scopeType,
        int? categoryId,
        int? itemModelId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);
}
