namespace RESQ.Domain.Enum.Logistics;

/// <summary>
/// Trạng thái của bản ghi đóng kho (depot_closures).
/// </summary>
public enum DepotClosureStatus
{
    /// <summary>Đang chờ admin chọn cách xử lý hàng tồn.</summary>
    InProgress,

    /// <summary>Đang được xử lý (đã được claim bởi một request — tránh race condition).</summary>
    Processing,

    /// <summary>Admin đã chọn phương án chuyển kho — đang chờ hai bên quản lý hoàn tất chuyển hàng. Không bị timeout.</summary>
    TransferPending,

    /// <summary>Đóng kho thành công.</summary>
    Completed,

    /// <summary>Admin đã huỷ yêu cầu đóng kho — kho khôi phục trạng thái cũ.</summary>
    Cancelled,

    /// <summary>Hết thời gian chờ (30 phút) — kho tự động khôi phục trạng thái cũ.</summary>
    TimedOut,

    /// <summary>Đóng kho thất bại sau nhiều lần thử — cần can thiệp thủ công.</summary>
    Failed
}
