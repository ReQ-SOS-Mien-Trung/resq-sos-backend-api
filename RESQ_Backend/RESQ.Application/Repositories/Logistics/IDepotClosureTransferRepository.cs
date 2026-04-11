using RESQ.Domain.Entities.Logistics;

namespace RESQ.Application.Repositories.Logistics;

public class DepotClosureTransferListItem
{
    public int TransferId { get; set; }
    public int ClosureId { get; set; }
    public int SourceDepotId { get; set; }
    public string SourceDepotName { get; set; } = string.Empty;
    public int TargetDepotId { get; set; }
    public string TargetDepotName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int SnapshotConsumableUnits { get; set; }
    public int SnapshotReusableUnits { get; set; }
    public DateTime? ShippedAt { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
}

/// <summary>
/// Repository quản lý bản ghi chuyển hàng khi đóng kho (depot_closure_transfers).
/// </summary>
public interface IDepotClosureTransferRepository
{
    Task<int> CreateAsync(DepotClosureTransferRecord record, CancellationToken cancellationToken = default);

    Task<DepotClosureTransferRecord?> GetByIdAsync(int transferId, CancellationToken cancellationToken = default);

    Task<DepotClosureTransferRecord?> GetByClosureIdAsync(int closureId, CancellationToken cancellationToken = default);

    /// <summary>Lấy transfer đang active theo closureId (chưa Cancelled/Received).</summary>
    Task<DepotClosureTransferRecord?> GetActiveByClosureIdAsync(int closureId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy transfer đang active mà kho <paramref name="targetDepotId"/> là kho đích nhận hàng.
    /// Dùng bởi manager kho đích để tự khám phá phiên chuyển hàng mà không cần biết closureId.
    /// Chỉ trả về transfer chưa kết thúc (chưa Received/Cancelled).
    /// </summary>
    Task<DepotClosureTransferRecord?> GetActiveIncomingByTargetDepotIdAsync(int targetDepotId, CancellationToken cancellationToken = default);

    Task<List<DepotClosureTransferListItem>> GetByRelatedDepotIdAsync(int depotId, CancellationToken cancellationToken = default);

    Task UpdateAsync(DepotClosureTransferRecord record, CancellationToken cancellationToken = default);
}
