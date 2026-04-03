namespace RESQ.Domain.Enum.Logistics;

/// <summary>
/// Trạng thái của quá trình chuyển hàng khi đóng kho.
/// </summary>
public enum DepotClosureTransferStatus
{
    /// <summary>Admin đã xác nhận kho đích — đang chờ kho nguồn xuất hàng.</summary>
    AwaitingShipment,

    /// <summary>Kho nguồn đã xuất hàng — đang vận chuyển, chờ kho đích xác nhận nhận.</summary>
    Shipping,

    /// <summary>Kho đích đã xác nhận nhận hàng — chuyển kho hoàn tất, kho nguồn đã đóng.</summary>
    Completed,

    /// <summary>Quá trình chuyển hàng đã bị huỷ.</summary>
    Cancelled
}
