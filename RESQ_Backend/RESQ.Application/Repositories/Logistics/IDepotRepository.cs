using RESQ.Application.Common.Models;
using RESQ.Application.UseCases.Logistics.Commands.InitiateDepotClosure;
using RESQ.Domain.Entities.Logistics;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.Repositories.Logistics
{
    public interface IDepotRepository 
    {
        Task CreateAsync(DepotModel depotModel, CancellationToken cancellationToken = default);
        Task UpdateAsync(DepotModel depotModel, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gán / d?i manager cho kho: unassign manager cu (n?u có), thêm b?n ghi manager m?i,
        /// c?p nh?t status kho ? Available.
        /// </summary>
        Task AssignManagerAsync(DepotModel depot, CancellationToken cancellationToken = default);

        /// <summary>
        /// G? manager hi?n t?i kh?i kho (soft-unassign): set UnassignedAt cho b?n ghi manager dang active,
        /// c?p nh?t status kho ? PendingAssignment. L?ch s? v?n du?c gi? l?i.
        /// </summary>
        Task UnassignManagerAsync(DepotModel depot, CancellationToken cancellationToken = default);
        
        // NEW: Pagination with optional status filter and full-text search
        Task<PagedResult<DepotModel>> GetAllPagedAsync(int pageNumber, int pageSize, IEnumerable<DepotStatus>? statuses = null, string? search = null, CancellationToken cancellationToken = default);
        
        // Legacy GetAll (optional, can be kept or removed)
        Task<IEnumerable<DepotModel>> GetAllAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Truy v?n t?t c? kho dang ho?t d?ng (Status = Available) và còn hàng (CurrentUtilization > 0)
        /// d? tính kho?ng cách và cung c?p thông tin cho AI l?p k? ho?ch c?u h?.
        /// </summary>
        Task<IEnumerable<DepotModel>> GetAvailableDepotsAsync(CancellationToken cancellationToken = default);
        
        Task<DepotModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<DepotModel?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

        // -- Depot Closure helpers ----------------------------------------------

        /// <summary>
        /// Ð?m s? kho dang ho?t d?ng (Available/Full/Closing) ngo?i tr? kho c?n dóng.
        /// Dùng d? ngan dóng kho duy nh?t còn l?i trong h? th?ng.
        /// </summary>
        Task<int> GetActiveDepotCountExcludingAsync(int depotId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Ki?m tra xem kho có yêu c?u ti?p t? nào chua k?t thúc (chua Completed/Rejected) không.
        /// Ki?m tra c? vai trò source_depot và requesting_depot.
        /// </summary>
        Task<(int AsSourceCount, int AsRequesterCount)> GetNonTerminalSupplyRequestCountsAsync(
            int depotId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Tính t?ng s? lu?ng consumable còn trong kho (sum of supply_inventory.quantity).
        /// Dùng cho snapshot và capacity check khi chuy?n kho.
        /// </summary>
        Task<decimal> GetConsumableTransferVolumeAsync(int depotId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Ð?m s? reusable items còn trong kho (không tính Decommissioned).
        /// Phân bi?t Available và InUse d? phát hi?n items dang ngoài nhi?m v?.
        /// </summary>
        Task<(int AvailableCount, int InUseCount)> GetReusableItemCountsAsync(
            int depotId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Ð?m s? supply_inventory rows có quantity > 0 (dùng cho TotalConsumableRows).
        /// </summary>
        Task<int> GetConsumableInventoryRowCountAsync(int depotId, CancellationToken cancellationToken = default);

        /// <summary>
        /// L?y tr?ng thái hi?n t?i c?a kho theo ID.
        /// Dùng d? ki?m tra kho có dang Closing/Closed tru?c khi cho phép import/export/transfer.
        /// Tr? v? null n?u không tìm th?y kho.
        /// </summary>
        Task<DepotStatus?> GetStatusByIdAsync(int depotId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Ki?m tra manager có dang active (UnassignedAt == null) ? m?t kho khác không.
        /// Dùng d? ngan gán m?t manager dang qu?n lý kho khác vào kho m?i.
        /// </summary>
        Task<bool> IsManagerActiveElsewhereAsync(Guid managerId, int excludeDepotId, CancellationToken cancellationToken = default);

        /// <summary>
        /// L?y chi ti?t t?n kho c?a kho (consumable + reusable) cho quy trình dóng kho.
        /// Dùng d? hi?n th? danh sách hàng còn trong kho khi admin mu?n dóng.
        /// </summary>
        Task<List<ClosureInventoryItemDto>> GetDetailedInventoryForClosureAsync(int depotId, CancellationToken cancellationToken = default);

        /// <summary>
        /// L?y chi ti?t t?n kho THEO T?NG LÔ (consumable = per-lot, reusable = grouped).
        /// Dùng cho file Excel template x? lý bên ngoài d? chia v?t ph?m theo lô.
        /// </summary>
        Task<List<ClosureInventoryLotItemDto>> GetLotDetailedInventoryForClosureAsync(int depotId, CancellationToken cancellationToken = default);
    }
}
