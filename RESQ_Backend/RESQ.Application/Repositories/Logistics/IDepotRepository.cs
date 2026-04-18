using RESQ.Application.Common.Models;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Logistics.Commands.InitiateDepotClosure;
using RESQ.Application.UseCases.Logistics.Queries.GetDepotManagers;
using RESQ.Domain.Entities.Logistics;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.Repositories.Logistics
{
    public interface IDepotRepository 
    {
        Task CreateAsync(DepotModel depotModel, CancellationToken cancellationToken = default);
        Task UpdateAsync(DepotModel depotModel, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gán thêm một manager mới cho kho mà không đụng vào các manager đang active khác.
        /// Cập nhật status kho → Available.
        /// </summary>
        Task AssignManagerAsync(DepotModel depot, Guid newManagerId, Guid? assignedBy = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gỡ manager hiện tại khỏi kho (soft-unassign): set UnassignedAt cho bản ghi manager đang active,
        /// cập nhật status kho → PendingAssignment. Lịch sử vẫn được giữ lại.
        /// </summary>
        Task UnassignManagerAsync(DepotModel depot, Guid? unassignedBy = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gỡ chỉ những manager theo danh sách userId được chỉ định (soft-unassign).
        /// Status kho được lấy từ domain object (có thể giữ Available nếu còn manager khác).
        /// </summary>
        Task UnassignSpecificManagersAsync(DepotModel depot, IReadOnlyList<Guid> userIds, Guid? unassignedBy = null, CancellationToken cancellationToken = default);
        
        // NEW: Pagination with optional status filter and full-text search
        Task<PagedResult<DepotModel>> GetAllPagedAsync(int pageNumber, int pageSize, IEnumerable<DepotStatus>? statuses = null, string? search = null, CancellationToken cancellationToken = default);
        
        // Legacy GetAll (optional, can be kept or removed)
        Task<IEnumerable<DepotModel>> GetAllAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Truy vấn tất cả kho đang hoạt động (Status = Available) và còn hàng (CurrentUtilization > 0)
        /// để tính khoảng cách và cung cấp thông tin cho AI lập kế hoạch cứu hộ.
        /// </summary>
        Task<IEnumerable<DepotModel>> GetAvailableDepotsAsync(CancellationToken cancellationToken = default);
        
        Task<DepotModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<DepotModel?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

        // -- Depot Closure helpers ----------------------------------------------

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
        Task<decimal> GetConsumableTransferVolumeAsync(int depotId, CancellationToken cancellationToken = default);

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

        /// <summary>
        /// Lấy trạng thái hiện tại của kho theo ID.
        /// Dùng để kiểm tra kho có đang Closing/Closed trước khi cho phép import/export/transfer.
        /// Trả về null nếu không tìm thấy kho.
        /// </summary>
        Task<DepotStatus?> GetStatusByIdAsync(int depotId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Lấy chi tiết tồn kho của kho (consumable + reusable) cho quy trình đóng kho.
        /// Dùng để hiển thị danh sách hàng còn trong kho khi admin muốn đóng.
        /// </summary>
        Task<List<ClosureInventoryItemDto>> GetDetailedInventoryForClosureAsync(int depotId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Lấy chi tiết tồn kho THEO TỪNG LÔ (consumable = per-lot, reusable = grouped).
        /// Dùng cho file Excel template xử lý bên ngoài để chia vật phẩm theo lô.
        /// </summary>
        Task<List<ClosureInventoryLotItemDto>> GetLotDetailedInventoryForClosureAsync(int depotId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Lấy danh sách tất cả kho mà user đang active quản lý (UnassignedAt IS NULL).
        /// Trả về trực tiếp DTO, dùng cho endpoint my-managed-depots.
        /// </summary>
        Task<List<ManagedDepotDto>> GetManagedDepotsByUserAsync(Guid userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Lấy danh sách manager đang active (UnassignedAt IS NULL) trong một kho.
        /// Dùng để frontend hiển thị danh sách quản kho hiện tại khi thao tác gán/gỡ.
        /// </summary>
        Task<List<DepotManagerInfoDto>> GetDepotManagersAsync(int depotId, CancellationToken cancellationToken = default);
    }
}
