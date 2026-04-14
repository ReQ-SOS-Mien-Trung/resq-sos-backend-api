using RESQ.Application.Common.Logistics;
using RESQ.Application.Common.Models;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.Repositories.Logistics;

public interface ISupplyRequestRepository
{
    Task<int> CreateAsync(
        int requestingDepotId,
        int sourceDepotId,
        List<(int ItemModelId, int Quantity)> items,
        SupplyRequestPriorityLevel priorityLevel,
        DateTime autoRejectAt,
        string? note,
        Guid requestedBy,
        CancellationToken cancellationToken = default);

    Task<SupplyRequestDetail?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<PagedResult<SupplyRequestListItem>> GetPagedByDepotsAsync(
        List<int> depotIds,
        string? sourceStatus,
        string? requestingStatus,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task UpdateStatusAsync(int id, string sourceStatus, string requestingStatus, string? rejectedReason, CancellationToken cancellationToken = default);

    Task<Guid?> GetActiveManagerUserIdByDepotIdAsync(int depotId, CancellationToken cancellationToken = default);

    /// <summary>
    /// <summary>
    /// Ð?t tr? (Reserve) v?t ph?m t?i kho ngu?n khi Accept.<br/>
    /// • Consumable: tang ReservedQuantity ? DepotSupplyInventory, log v?i DepotSupplyInventoryId.<br/>
    /// • Reusable: ch?n N don v? Status=Available ? Status=Reserved + SupplyRequestId, log m?t b?n ghi per unit v?i ReusableItemId.<br/>
    /// Throws BadRequestException n?u không d? hàng kh? d?ng.
    /// </summary>
    Task ReserveItemsAsync(int sourceDepotId, List<(int ItemModelId, int Quantity)> items, int supplyRequestId, Guid performedBy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Xu?t kho (TransferOut) t? kho ngu?n khi Ship.<br/>
    /// • Consumable: gi?m Quantity + ReservedQuantity ? DepotSupplyInventory, log v?i DepotSupplyInventoryId.<br/>
    /// • Reusable: chuy?n Status=Reserved ? InTransit, DepotId = null, log per unit v?i ReusableItemId.<br/>
    /// Throws BadRequestException n?u s? don v? d?t tr? không kh?p.
    /// </summary>
    Task TransferOutAsync(int sourceDepotId, List<(int ItemModelId, int Quantity)> items, int supplyRequestId, Guid performedBy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Nh?p kho (TransferIn) cho kho yêu c?u khi Confirm/Received.<br/>
    /// • Consumable: tang Quantity ? DepotSupplyInventory t?i kho dích (t?o m?i n?u chua có), log v?i DepotSupplyInventoryId.<br/>
    /// • Reusable: chuy?n Status=InTransit ? Available, DepotId = requestingDepotId, SupplyRequestId = null, log per unit v?i ReusableItemId.<br/>
    /// Throws BadRequestException n?u s? don v? InTransit không kh?p.
    /// </summary>
    Task TransferInAsync(int requestingDepotId, List<(int ItemModelId, int Quantity)> items, int supplyRequestId, Guid performedBy, CancellationToken cancellationToken = default);

    Task<List<PendingSupplyRequestMonitorItem>> GetPendingForMonitoringAsync(CancellationToken cancellationToken = default);

    Task SetAutoRejectAtAsync(int id, DateTime autoRejectAt, CancellationToken cancellationToken = default);

    Task MarkHighEscalationNotifiedAsync(int id, CancellationToken cancellationToken = default);

    Task MarkUrgentEscalationNotifiedAsync(int id, CancellationToken cancellationToken = default);

    Task<bool> AutoRejectIfPendingAsync(int id, string rejectedReason, CancellationToken cancellationToken = default);

    /// <summary>
    /// L?y danh sách t?t c? yêu c?u ti?p t? liên quan d?n m?t t?p depot (c? 2 chi?u).
    /// Bao g?m c? các yêu c?u dã hoàn thành (Completed/Received) và b? t? ch?i.
    /// </summary>
    Task<List<DepotRequestItem>> GetRequestsByDepotIdsAsync(
        List<int> depotIds,
        CancellationToken cancellationToken = default);
}
