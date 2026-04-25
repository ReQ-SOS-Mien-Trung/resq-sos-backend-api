using RESQ.Domain.Entities.Logistics.Models;
using RESQ.Domain.Entities.Logistics.ValueObjects;

namespace RESQ.Application.Repositories.Logistics;

public interface IInventoryMovementExportRepository
{
    /// <summary>
    /// Truy vấn tất cả dòng biến động kho trong khoảng thời gian cho trước,
    /// lọc theo kho cụ thể (nếu có). Kết quả đã được sắp xếp theo thời gian tăng dần.
    /// </summary>
    Task<List<InventoryMovementRow>> GetMovementRowsAsync(
        InventoryMovementExportPeriod period,
        int? depotId,
        int? itemModelId,
        CancellationToken cancellationToken = default);
}
