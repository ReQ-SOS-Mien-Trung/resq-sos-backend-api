namespace RESQ.Domain.Enum.Logistics;

/// <summary>
/// Trạng thái của quá trình chuyển hàng khi đóng kho.
/// </summary>
public enum DepotClosureTransferStatus
{
    /// <summary>Admin đã tạo chuyển kho — đang chờ kho nguồn xác nhận chuẩn bị.</summary>
    AwaitingPreparation,

    /// <summary>Kho nguồn đang chuẩn bị hàng.</summary>
    Preparing,

    /// <summary>Kho nguồn đã xuất hàng — đang vận chuyển, chờ kho nguồn xác nhận giao xong.</summary>
    Shipping,

    /// <summary>Kho nguồn đã xác nhận giao hàng — chờ kho đích xác nhận nhận.</summary>
    Completed,

    /// <summary>Kho đích đã xác nhận nhận hàng — chuyển kho hoàn tất, kho nguồn đã đóng.</summary>
    Received,

    /// <summary>Quá trình chuyển hàng đã bị huỷ.</summary>
    Cancelled
}
