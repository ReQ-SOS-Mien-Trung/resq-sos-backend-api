using RESQ.Domain.Entities.Logistics.ValueObjects;

namespace RESQ.Application.Services;

public interface IStockWarningEvaluatorService
{
    /// <summary>
    /// Đánh giá mức cảnh báo tồn kho cho một item tại một depot.
    /// Tự động resolve threshold và load warning bands từ cache.
    /// Trả về UNCONFIGURED nếu không có threshold được cấu hình.
    /// </summary>
    Task<StockWarningResult> EvaluateAsync(
        int depotId,
        int? categoryId,
        int itemModelId,
        int availableQty,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Xoá cache warning bands để force reload từ DB lần tiếp theo.
    /// Gọi sau khi Admin cập nhật cấu hình bands.
    /// </summary>
    Task InvalidateBandCacheAsync();
}
