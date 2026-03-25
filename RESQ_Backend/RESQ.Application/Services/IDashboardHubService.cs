namespace RESQ.Application.Services;

public interface IDashboardHubService
{
    /// <summary>
    /// Đẩy dữ liệu biến động victim (6 tháng gần nhất, group by month)
    /// tới tất cả admin đang kết nối dashboard hub.
    /// </summary>
    Task PushVictimsByPeriodAsync(CancellationToken cancellationToken = default);
}
