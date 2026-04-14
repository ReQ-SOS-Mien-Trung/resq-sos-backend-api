using RESQ.Application.Common.Models;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Logistics.Queries.GetDepotInventoryByCategory;
using RESQ.Application.UseCases.Logistics.Queries.GetLowStockItems;
using RESQ.Application.UseCases.Logistics.Queries.SearchWarehousesByItems;
using RESQ.Domain.Entities.Logistics;
using RESQ.Domain.Entities.Logistics.Models;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.Repositories.Logistics;

public interface IDepotInventoryRepository
{
    Task<int?> GetActiveDepotIdByManagerAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<List<int>> GetActiveDepotIdsByManagerAsync(Guid userId, CancellationToken cancellationToken = default);
    
    Task<PagedResult<InventoryItemModel>> GetInventoryPagedAsync(
        int depotId,
        List<int>? categoryIds,
        List<ItemType>? itemTypes,
        List<TargetGroup>? targetGroups,
        string? itemName,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// L?y danh sách các lô hàng (lot) c?a m?t m?t hàng t?i kho, s?p x?p theo FEFO.
    /// </summary>
    Task<PagedResult<InventoryLotModel>> GetInventoryLotsAsync(
        int depotId, int itemModelId, int pageNumber, int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// L?y t?ng s? lu?ng t?n kho theo danh m?c c?a m?t kho c? th?.
    /// </summary>
    Task<List<DepotCategoryQuantityDto>> GetInventoryByCategoryAsync(int depotId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tìm ki?m v?t ph?m theo t? khoá danh m?c/lo?i d? agent AI dùng trong quá trình l?p k? ho?ch.
    /// Tr? v? c? Consumable l?n Reusable; v?i Reusable, AvailableQuantity là s? don v? Available.
    /// </summary>
    Task<(List<AgentInventoryItem> Items, int TotalCount)> SearchForAgentAsync(
        string categoryKeyword,
        string? typeKeyword,
        int page,
        int pageSize,
        IReadOnlyCollection<int>? allowedDepotIds = null,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the lat/lng of a depot by its ID, or null if not found / no location set.
    /// </summary>
    Task<(double Latitude, double Longitude)?> GetDepotLocationAsync(int depotId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tìm ki?m các kho c?u tr? có ch?a v?t ph?m theo danh sách mã v?t ph?m.
    /// Ch? tr? v? kho có s? lu?ng kh? d?ng >= quantity.
    /// Tr? v? danh sách ph?ng (item, depot) d? handler nhóm l?i.
    /// </summary>
    Task<(List<WarehouseItemRow> Rows, int TotalItemCount)> SearchWarehousesByItemsAsync(
        List<int>? itemModelIds,
        Dictionary<int, int> itemQuantities,
        bool activeDepotsOnly,
        int? excludeDepotId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ki?m tra t?n kho t?i m?t kho c? th? cho danh sách v?t ph?m.
    /// Tr? v? danh sách v?t ph?m không d? s? lu?ng ho?c không có trong kho.
    /// S? lu?ng kh? d?ng = Quantity - ReservedQuantity.
    /// </summary>
    Task<List<SupplyShortageResult>> CheckSupplyAvailabilityAsync(
        int depotId,
        List<(int ItemModelId, string ItemName, int RequestedQuantity)> items,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ð?t tru?c v?t ph?m cho mission: tang ReservedQuantity và tr? v? snapshot
    /// các lô FEFO ho?c reusable units activity c?n l?y.
    /// </summary>
    Task<MissionSupplyReservationResult> ReserveSuppliesAsync(
        int depotId,
        List<(int ItemModelId, int Quantity)> items,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Team xác nh?n dã l?y hàng: gi?m c? Quantity và ReservedQuantity, ghi InventoryLog,
    /// d?ng th?i tr? v? chi ti?t th?c t? dã l?y theo lot FEFO ho?c reusable unit.
    /// </summary>
    Task<MissionSupplyPickupExecutionResult> ConsumeReservedSuppliesAsync(
        int depotId,
        List<(int ItemModelId, int Quantity)> items,
        Guid performedBy,
        int activityId,
        int missionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Depot manager xác nh?n nh?n l?i v?t ph?m t? mission và nh?p kho theo d? li?u th?c t?.
    /// Consumable du?c nh?p l?i theo quantity; Reusable du?c nh?n l?i theo t?ng unit id,
    /// ho?c quantity fallback cho legacy mission chua có unit snapshot.
    /// Condition và Note c?a t?ng reusable unit s? du?c c?p nh?t n?u du?c cung c?p.
    /// </summary>
    Task<MissionSupplyReturnExecutionResult> ReceiveMissionReturnAsync(
        int depotId,
        int missionId,
        int activityId,
        Guid performedBy,
        List<(int ItemModelId, int Quantity)> consumableItems,
        List<(int ReusableItemId, string? Condition, string? Note)> reusableItems,
        List<(int ItemModelId, int Quantity)> legacyReusableQuantities,
        string? discrepancyNote,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// L?y d? li?u thô v?t ph?m tiêu hao d? application/domain t? resolve threshold và phân lo?i m?c t?n.
    /// </summary>
    Task<List<LowStockRawItemDto>> GetLowStockRawItemsAsync(
        int? depotId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gi?i phóng v?t ph?m dã d?t tru?c (ví d? khi hu? activity ho?c thay d?i items): gi?m ReservedQuantity.
    /// </summary>
    Task ReleaseReservedSuppliesAsync(
        int depotId,
        List<(int ItemModelId, int Quantity)> items,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Xu?t kho th? công (Export): gi?m Quantity, áp d?ng FEFO trên các lô và ghi InventoryLog.
    /// Ch? du?c xu?t s? lu?ng kh? d?ng (Quantity - ReservedQuantity).
    /// </summary>
    Task ExportInventoryAsync(
        int depotId,
        int itemModelId,
        int quantity,
        Guid performedBy,
        string? note,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ði?u ch?nh t?n kho (Adjust): quantityChange duong ? t?o lô m?i + tang Quantity;
    /// quantityChange âm ? FEFO deduction trên các lô + gi?m Quantity.
    /// </summary>
    Task AdjustInventoryAsync(
        int depotId,
        int itemModelId,
        int quantityChange,
        Guid performedBy,
        string reason,
        string? note,
        DateTime? expiredDate,
        CancellationToken cancellationToken = default);

    // -- Depot Closure helpers ------------------------------------------------

    /// <summary>
    /// Chuy?n toàn b? inventory t? kho dóng sang kho dích.
    /// Consumable: upsert supply_inventory t?i dích + chuy?n t?t c? lots + ghi log.
    /// Reusable (Available): c?p nh?t depot_id sang kho dích + ghi log.
    /// Reusable (InUse): dánh d?u pending_reassignment, không di chuy?n ngay.
    /// X? lý theo cursor-based batch d? có th? resume khi retry.
    /// Returns (processedRows, lastInventoryId) d? handler luu ti?n trình.
    /// </summary>
    Task<(int ProcessedRows, int? LastInventoryId)> BulkTransferForClosureAsync(
        int sourceDepotId,
        int targetDepotId,
        int closureId,
        Guid performedBy,
        int? lastProcessedInventoryId = null,
        int batchSize = 100,
        CancellationToken cancellationToken = default);

    Task TransferClosureItemsAsync(
        int sourceDepotId,
        int targetDepotId,
        int closureId,
        int transferId,
        Guid performedBy,
        IReadOnlyCollection<DepotClosureTransferItemMoveDto> items,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tra c?u userId c?a qu?n lý dang du?c phân công t?i kho (ngu?c v?i GetActiveDepotIdByManagerAsync).
    /// </summary>
    Task<Guid?> GetActiveManagerUserIdByDepotIdAsync(int depotId, CancellationToken ct = default);

    /// <summary>
    /// Zero-out toàn b? inventory khi dóng kho theo hình th?c x? lý bên ngoài.
    /// Ghi log d?y d? t?ng lô hàng v?i closure_id d? audit.
    /// Reusable (Available): chuy?n sang Decommissioned.
    /// Reusable (InUse): dánh d?u pending_reassignment.
    /// </summary>
    Task ZeroOutForClosureAsync(
        int depotId,
        int closureId,
        Guid performedBy,
        string? note,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ki?m tra xem kho có dang có cam k?t t?n kho chua hoàn t?t không:
    /// - Consumable: mission_reserved_quantity > 0 (dang du?c d?t cho nhi?m v? c?u h?).
    /// - Reusable: status = 'InUse' t?i kho này (dang du?c s? d?ng trong nhi?m v?).
    /// Dùng d? ch?n chuy?n sang Unavailable khi còn ho?t d?ng dang di?n ra.
    /// </summary>
    Task<bool> HasActiveInventoryCommitmentsAsync(int depotId, CancellationToken cancellationToken = default);
}

public class DepotClosureTransferItemMoveDto
{
    public int ItemModelId { get; set; }
    public string ItemType { get; set; } = string.Empty;
    public int Quantity { get; set; }
}
