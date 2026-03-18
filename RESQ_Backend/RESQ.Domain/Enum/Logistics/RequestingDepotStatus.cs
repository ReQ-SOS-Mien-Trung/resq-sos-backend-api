namespace RESQ.Domain.Enum.Logistics;

/// <summary>Trạng thái từ góc nhìn kho yêu cầu (requesting depot).</summary>
public enum RequestingDepotStatus
{
    /// <summary>Yêu cầu đã gửi — chờ kho nguồn phê duyệt ↔ SourceDepotStatus: <see cref="SourceDepotStatus.Pending"/>.</summary>
    WaitingForApproval = 0,

    /// <summary>Kho nguồn đã chấp nhận ↔ SourceDepotStatus: <see cref="SourceDepotStatus.Accepted"/> / <see cref="SourceDepotStatus.Preparing"/>.</summary>
    Approved           = 1,

    /// <summary>Hàng đang trên đường vận chuyển ↔ SourceDepotStatus: <see cref="SourceDepotStatus.Shipping"/>.</summary>
    InTransit          = 2,

    /// <summary>Kho yêu cầu đã nhận đủ hàng ↔ SourceDepotStatus: <see cref="SourceDepotStatus.Completed"/>.</summary>
    Received           = 3,

    /// <summary>Kho nguồn từ chối — yêu cầu bị huỷ ↔ SourceDepotStatus: <see cref="SourceDepotStatus.Rejected"/>.</summary>
    Rejected           = 4
}
