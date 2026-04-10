using RESQ.Domain.Entities.Logistics;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.Repositories.Logistics;

/// <summary>
/// Repository quản lý bản ghi đóng kho (depot_closures).
/// </summary>
public interface IDepotClosureRepository
{
    /// <summary>Tạo bản ghi đóng kho mới, trả về ID.</summary>
    Task<int> CreateAsync(DepotClosureRecord record, CancellationToken cancellationToken = default);

    /// <summary>Lấy bản ghi đóng kho theo ID.</summary>
    Task<DepotClosureRecord?> GetByIdAsync(int closureId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy bản ghi đóng kho đang active (InProgress/Processing) của một kho.
    /// Dùng để check idempotency ở Phase 1.
    /// </summary>
    Task<DepotClosureRecord?> GetActiveClosureByDepotIdAsync(int depotId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy tất cả bản ghi InProgress đã quá thời gian timeout.
    /// Dùng bởi DepotClosureTimeoutBackgroundService.
    /// </summary>
    Task<List<DepotClosureRecord>> GetTimedOutClosuresAsync(CancellationToken cancellationToken = default);

    /// <summary>Cập nhật bản ghi đóng kho.</summary>
    Task UpdateAsync(DepotClosureRecord record, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomic claim: chuyển trạng thái InProgress → Processing để tránh race condition.
    /// Trả về true nếu claim thành công, false nếu đã bị claim bởi request khác.
    /// </summary>
    Task<bool> TryClaimForProcessingAsync(int closureId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Hoàn tác claim: chuyển Processing → InProgress để cho phép user retry
    /// sau khi xảy ra lỗi validation (ConflictException / NotFoundException).
    /// </summary>
    Task ResetProcessingToInProgressAsync(int closureId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tái claim bản ghi bị kẹt tại Processing bằng optimistic concurrency (kiểm tra rowVersion).
    /// An toàn khi nhiều request cùng cố recover đồng thời: chỉ đúng 1 request thành công.
    /// Trả về true nếu claim thành công.
    /// </summary>
    Task<bool> TryForceClaimFromProcessingAsync(int closureId, int expectedRowVersion, CancellationToken cancellationToken = default);

    /// <summary>Cập nhật tiến độ batch processing (cursor + count).</summary>
    Task UpdateProgressAsync(int closureId, int processedRows, int lastInventoryId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy toàn bộ lịch sử phiên đóng kho của một kho, sắp xếp theo thời gian khởi tạo giảm dần.
    /// Kèm thông tin kho đích (nếu TransferToDepot).
    /// </summary>
    Task<List<DepotClosureListItem>> GetClosuresByDepotIdAsync(int depotId, CancellationToken cancellationToken = default);
    Task<DepotClosureListItem?> GetClosureDetailAsync(int depotId, int closureId, CancellationToken cancellationToken = default);
}
