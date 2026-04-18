namespace RESQ.Domain.Enum.Logistics;

/// <summary>
/// Cách admin chọn để giải quyết hàng tồn kho khi đóng kho.
/// </summary>
public enum CloseResolutionType
{
    /// <summary>Chuyển toàn bộ hàng sang một kho khác.</summary>
    TransferToDepot,

    /// <summary>Xử lý hàng bên ngoài (quyên góp, thanh lý, trả nhà cung cấp...).</summary>
    ExternalResolution
}
