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
        string? roleFilter,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task UpdateStatusAsync(int id, string sourceStatus, string requestingStatus, string? rejectedReason, Guid? performedBy = null, CancellationToken cancellationToken = default);

    Task<Guid?> GetActiveManagerUserIdByDepotIdAsync(int depotId, CancellationToken cancellationToken = default);

    /// <summary>
    /// <summary>
    /// Đặt trữ (Reserve) vật phẩm tại kho nguồn khi Prepare.<br/>
    /// • Consumable: tăng TransferReservedQuantity và tạo reservation row theo đúng lot/legacy inventory.<br/>
    /// • Reusable: chọn đúng từng unit Status=Available → Status=Reserved và tạo bản ghi link riêng cho supply request.<br/>
    /// Throws BadRequestException nếu không đủ hàng khả dụng.
    /// </summary>
    Task ReserveItemsAsync(int sourceDepotId, List<(int ItemModelId, int Quantity)> items, int supplyRequestId, Guid performedBy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Xuất kho (TransferOut) từ kho nguồn khi Ship.<br/>
    /// • Consumable: trừ Quantity tại kho nguồn, giảm TransferReservedQuantity, trừ đúng từng lot đã reserve và ghi log đầy đủ.<br/>
    /// • Reusable: chuyển Status=Reserved → InTransit, DepotId = null, log per unit với ReusableItemId.<br/>
    /// Throws nếu reservation không khớp hoặc source inventory không còn đủ hàng.
    /// </summary>
    Task TransferOutAsync(int sourceDepotId, List<(int ItemModelId, int Quantity)> items, int supplyRequestId, Guid performedBy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Nhập kho (TransferIn) cho kho yêu cầu khi Confirm/Received.<br/>
    /// • Consumable: tăng Quantity ở kho đích và tạo lot mới từ đúng reservation snapshot đã ship.<br/>
    /// • Reusable: chuyển Status=InTransit → Available, DepotId = requestingDepotId, log per unit với ReusableItemId.<br/>
    /// Throws nếu shipment manifest không khớp.
    /// </summary>
    Task TransferInAsync(int requestingDepotId, List<(int ItemModelId, int Quantity)> items, int supplyRequestId, Guid performedBy, CancellationToken cancellationToken = default);

    Task<List<PendingSupplyRequestMonitorItem>> GetPendingForMonitoringAsync(CancellationToken cancellationToken = default);

    Task SetAutoRejectAtAsync(int id, DateTime autoRejectAt, CancellationToken cancellationToken = default);

    Task MarkHighEscalationNotifiedAsync(int id, CancellationToken cancellationToken = default);

    Task MarkUrgentEscalationNotifiedAsync(int id, CancellationToken cancellationToken = default);

    Task<bool> AutoRejectIfPendingAsync(int id, string rejectedReason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy danh sách tất cả yêu cầu tiếp tế liên quan đến một tập depot (cả 2 chiều).
    /// Bao gồm cả các yêu cầu đã hoàn thành (Completed/Received) và bị từ chối.
    /// </summary>
    Task<List<DepotRequestItem>> GetRequestsByDepotIdsAsync(
        List<int> depotIds,
        CancellationToken cancellationToken = default);
}
