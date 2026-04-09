using RESQ.Domain.Entities.Finance;

namespace RESQ.Application.Services;

/// <summary>
/// Chuyển toàn bộ số dư quỹ kho (từ mọi nguồn) về quỹ hệ thống khi đóng kho.
/// </summary>
public interface IDepotFundDrainService
{
    /// <summary>
    /// Drain tất cả depot fund (balance > 0) của kho về system fund.
    /// Tạo transaction cho cả 2 phía (depot fund + system fund).
    /// </summary>
    /// <param name="depotId">Depot đang đóng.</param>
    /// <param name="closureId">Closure record ID (dùng làm reference).</param>
    /// <param name="performedBy">User thực hiện.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tổng tiền đã drain.</returns>
    Task<decimal> DrainAllToSystemFundAsync(int depotId, int closureId, Guid performedBy, CancellationToken cancellationToken = default);
}
