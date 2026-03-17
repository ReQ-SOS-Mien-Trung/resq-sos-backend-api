using RESQ.Application.Common.Models;

namespace RESQ.Application.Repositories.Logistics;

public class SupplyRequestDetail
{
    public int Id { get; set; }
    public int RequestingDepotId { get; set; }
    public int SourceDepotId { get; set; }
    public string SourceStatus { get; set; } = string.Empty;
    public string RequestingStatus { get; set; } = string.Empty;
    public Guid RequestedBy { get; set; }
    public List<(int ItemModelId, int Quantity)> Items { get; set; } = new();
}

public class SupplyRequestItemDetail
{
    public int ItemModelId { get; set; }
    public string? ItemModelName { get; set; }
    public string? Unit { get; set; }
    public int Quantity { get; set; }
}

public class SupplyRequestListItem
{
    public int Id { get; set; }
    public int RequestingDepotId { get; set; }
    public string? RequestingDepotName { get; set; }
    public int SourceDepotId { get; set; }
    public string? SourceDepotName { get; set; }
    public string SourceStatus { get; set; } = string.Empty;
    public string RequestingStatus { get; set; } = string.Empty;
    public string? Note { get; set; }
    public string? RejectedReason { get; set; }
    public Guid RequestedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? RespondedAt { get; set; }
    public DateTime? ShippedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public List<SupplyRequestItemDetail> Items { get; set; } = new();
}

public interface ISupplyRequestRepository
{
    Task<int> CreateAsync(
        int requestingDepotId,
        int sourceDepotId,
        List<(int ItemModelId, int Quantity)> items,
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
    /// Xuất kho (TransferOut) từ kho nguồn khi ship.
    /// Giảm Quantity ở DepotSupplyInventory, tạo InventoryLog.
    /// </summary>
    Task TransferOutAsync(int sourceDepotId, List<(int ItemModelId, int Quantity)> items, int supplyRequestId, Guid performedBy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Nhập kho (TransferIn) cho kho yêu cầu khi confirm.
    /// Tăng Quantity ở DepotSupplyInventory (tạo mới nếu chưa có), tạo InventoryLog.
    /// </summary>
    Task TransferInAsync(int requestingDepotId, List<(int ItemModelId, int Quantity)> items, int supplyRequestId, Guid performedBy, CancellationToken cancellationToken = default);
}
