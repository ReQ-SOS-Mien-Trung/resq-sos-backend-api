namespace RESQ.Domain.Enum.Logistics;

/// <summary>Trạng thái từ góc nhìn kho nguồn (source depot).</summary>
public enum SourceDepotStatus
{
    /// <summary>Yêu cầu mới gửi đến — chờ kho nguồn xem xét.</summary>
    Pending   = 0,

    /// <summary>Kho nguồn đã chấp nhận yêu cầu — chờ bắt đầu chuẩn bị hàng.</summary>
    Accepted  = 1,

    /// <summary>Kho nguồn đang đóng gói / picking — chưa xuất kho.</summary>
    Preparing = 2,

    /// <summary>Đã xuất kho và đang vận chuyển → RequestingDepotStatus: <see cref="RequestingDepotStatus.InTransit"/>.</summary>
    Shipped   = 3,

    /// <summary>Kho yêu cầu đã xác nhận nhận hàng → RequestingDepotStatus: <see cref="RequestingDepotStatus.Received"/>.</summary>
    Completed = 4,

    /// <summary>Kho nguồn từ chối yêu cầu → RequestingDepotStatus: <see cref="RequestingDepotStatus.Rejected"/>.</summary>
    Rejected  = 5
}
