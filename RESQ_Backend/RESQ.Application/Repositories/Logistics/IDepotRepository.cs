using RESQ.Application.Common.Models;
using RESQ.Domain.Entities.Logistics;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.Repositories.Logistics
{
    public interface IDepotRepository 
    {
        Task CreateAsync(DepotModel depotModel, CancellationToken cancellationToken = default);
        Task UpdateAsync(DepotModel depotModel, CancellationToken cancellationToken = default);
        
        // NEW: Pagination with optional status filter
        Task<PagedResult<DepotModel>> GetAllPagedAsync(int pageNumber, int pageSize, IEnumerable<DepotStatus>? statuses = null, CancellationToken cancellationToken = default);
        
        // Legacy GetAll (optional, can be kept or removed)
        Task<IEnumerable<DepotModel>> GetAllAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Truy vấn tất cả kho đang hoạt động (Status = Available) và còn hàng (CurrentUtilization > 0)
        /// để tính khoảng cách và cung cấp thông tin cho AI lập kế hoạch cứu hộ.
        /// </summary>
        Task<IEnumerable<DepotModel>> GetAvailableDepotsAsync(CancellationToken cancellationToken = default);
        
        Task<DepotModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<DepotModel?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

        // ── Depot Closure helpers ──────────────────────────────────────────────

        /// <summary>
        /// Đếm số kho đang hoạt động (Available/Full/Closing) ngoại trừ kho cần đóng.
        /// Dùng để ngăn đóng kho duy nhất còn lại trong hệ thống.
        /// </summary>
        Task<int> GetActiveDepotCountExcludingAsync(int depotId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Kiểm tra xem kho có yêu cầu tiếp tế nào chưa kết thúc (chưa Completed/Rejected) không.
        /// Kiểm tra cả vai trò source_depot và requesting_depot.
        /// </summary>
        Task<(int AsSourceCount, int AsRequesterCount)> GetNonTerminalSupplyRequestCountsAsync(
            int depotId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Tính tổng số lượng consumable còn trong kho (sum of supply_inventory.quantity).
        /// Dùng cho snapshot và capacity check khi chuyển kho.
        /// </summary>
        Task<int> GetConsumableTransferVolumeAsync(int depotId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Đếm số reusable items còn trong kho (không tính Decommissioned).
        /// Phân biệt Available và InUse để phát hiện items đang ngoài nhiệm vụ.
        /// </summary>
        Task<(int AvailableCount, int InUseCount)> GetReusableItemCountsAsync(
            int depotId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Đếm số supply_inventory rows có quantity > 0 (dùng cho TotalConsumableRows).
        /// </summary>
        Task<int> GetConsumableInventoryRowCountAsync(int depotId, CancellationToken cancellationToken = default);
    }
}
