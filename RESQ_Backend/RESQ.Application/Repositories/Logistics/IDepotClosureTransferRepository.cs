using RESQ.Domain.Entities.Logistics;

namespace RESQ.Application.Repositories.Logistics;

/// <summary>
/// Repository quản lý bản ghi chuyển hàng khi đóng kho (depot_closure_transfers).
/// </summary>
public interface IDepotClosureTransferRepository
{
    Task<int> CreateAsync(DepotClosureTransferRecord record, CancellationToken cancellationToken = default);

    Task<DepotClosureTransferRecord?> GetByIdAsync(int transferId, CancellationToken cancellationToken = default);

    /// <summary>Lấy transfer đang active theo closureId (chưa Cancelled/Completed).</summary>
    Task<DepotClosureTransferRecord?> GetActiveByClosureIdAsync(int closureId, CancellationToken cancellationToken = default);

    Task UpdateAsync(DepotClosureTransferRecord record, CancellationToken cancellationToken = default);
}
