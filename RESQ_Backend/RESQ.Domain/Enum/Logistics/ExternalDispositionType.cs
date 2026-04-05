namespace RESQ.Domain.Enum.Logistics;

/// <summary>
/// Hình thức xử lý hàng tồn kho bên ngoài khi đóng kho.
/// </summary>
public enum ExternalDispositionType
{
    /// <summary>Quyên góp cho tổ chức từ thiện / nhân đạo.</summary>
    DonatedToOrganization,

    /// <summary>Trả lại nhà cung cấp.</summary>
    ReturnedToSupplier,

    /// <summary>Thanh lý / tiêu hủy tại chỗ.</summary>
    Disposed,

    /// <summary>Bàn giao cho cơ quan nhà nước.</summary>
    TransferredToGovernment,

    /// <summary>Hình thức khác (bắt buộc điền ghi chú).</summary>
    Other
}
