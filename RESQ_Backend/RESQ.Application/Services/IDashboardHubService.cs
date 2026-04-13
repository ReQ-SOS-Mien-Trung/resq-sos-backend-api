namespace RESQ.Application.Services;

public interface IDashboardHubService
{
    /// <summary>
    /// Đẩy dữ liệu biến động victim (6 tháng gần nhất, group by month)
    /// tới tất cả admin đang kết nối dashboard hub.
    /// </summary>
    Task PushVictimsByPeriodAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Đẩy snapshot realtime của một điểm tập kết tới dashboard admin.
    /// Dùng để cập nhật trạng thái, số mission activity đang hướng về AP và cảnh báo reroute.
    /// </summary>
    Task PushAssemblyPointSnapshotAsync(int assemblyPointId, string operation, CancellationToken cancellationToken = default);
}
