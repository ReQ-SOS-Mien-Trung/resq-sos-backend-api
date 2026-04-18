namespace RESQ.Application.Services;

/// <summary>
/// Dịch vụ đẩy real-time cho các endpoint vận hành:
/// - /personnel/assembly-point (danh sách điểm tập kết)
/// - /logistics/inventory/depot/{id} (tồn kho kho cụ thể)
/// - /emergency/sos-clusters/{clusterId}/alternative-depots
/// - /logistics/rescue-team/by-cluster/{clusterId}  (personnel/rescue-teams/by-cluster)
/// - /logistics/depot/by-cluster/{clusterId}
/// 
/// Hub endpoint: /hubs/operational
/// </summary>
public interface IOperationalHubService
{
    /// <summary>
    /// Thông báo danh sách điểm tập kết có thay đổi.
    /// Push event "ReceiveAssemblyPointListUpdate" tới group "operational:assembly-points".
    /// Trigger: tạo, cập nhật, đổi status AP; gán/gỡ rescuer; check-in/out.
    /// </summary>
    Task PushAssemblyPointListUpdateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Thông báo tồn kho của một kho đã thay đổi.
    /// Push event "ReceiveDepotInventoryUpdate" tới:
    ///   - group "operational:logistics" (tất cả client logistics)
    ///   - group "operational:depot:{depotId}" (client đang xem kho này)
    /// Trigger: xác nhận nhận hàng, hoàn trả, phân bổ, điều chỉnh tồn kho.
    /// </summary>
    Task PushDepotInventoryUpdateAsync(int depotId, string operation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Thông báo dữ liệu logistics liên quan đến cluster đã thay đổi.
    /// Push event "ReceiveLogisticsUpdate" tới:
    ///   - group "operational:logistics" (broadcast toàn bộ)
    ///   - group "operational:cluster:{clusterId}" nếu clusterId được cung cấp
    /// Trigger: đổi trạng thái đội (mission state), đổi trạng thái kho, thay đổi tồn kho.
    /// </summary>
    /// <param name="resourceType">
    /// Loại tài nguyên bị ảnh hưởng: "rescue-teams" | "depots" | "alternative-depots" | "all"
    /// </param>
    /// <param name="clusterId">ID cluster cụ thể (null = ảnh hưởng tất cả cluster)</param>
    Task PushLogisticsUpdateAsync(string resourceType, int? clusterId = null, CancellationToken cancellationToken = default);
}
