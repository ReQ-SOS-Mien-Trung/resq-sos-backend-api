using RESQ.Domain.Entities.Logistics;

namespace RESQ.Application.Repositories.Logistics;

/// <summary>
/// Repository quản lý bản ghi chuyển hàng khi đóng kho (depot_closure_transfers).
/// </summary>
public interface IDepotClosureTransferRepository
{
    Task<int> CreateAsync(DepotClosureTransferRecord record, CancellationToken cancellationToken = default);

    Task<DepotClosureTransferRecord?> GetByIdAsync(int transferId, CancellationToken cancellationToken = default);

    /// <summary>Lấy transfer đang active theo closureId (chưa Cancelled/Received).</summary>
    Task<DepotClosureTransferRecord?> GetActiveByClosureIdAsync(int closureId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy transfer đang active mà kho <paramref name="targetDepotId"/> là kho đích nhận hàng.
    /// Dùng bởi manager kho đích để tự khám phá phiên chuyển hàng mà không cần biết closureId.
    /// Chỉ trả về transfer chưa kết thúc (chưa Received/Cancelled).
    /// </summary>
    Task<DepotClosureTransferRecord?> GetActiveIncomingByTargetDepotIdAsync(int targetDepotId, CancellationToken cancellationToken = default);

    Task UpdateAsync(DepotClosureTransferRecord record, CancellationToken cancellationToken = default);
}
