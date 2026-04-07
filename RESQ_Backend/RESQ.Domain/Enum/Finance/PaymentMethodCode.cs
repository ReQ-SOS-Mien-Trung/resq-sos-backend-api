using System.ComponentModel;

namespace RESQ.Domain.Enum.Finance;

public enum PaymentMethodCode
{
    [Description("Chuyển khoản Ngân hàng (QR Code)")]
    PAYOS = 1,

    /// <summary>
    /// MoMo hiện chưa được hỗ trợ cho đơn mới. Giữ nguyên để tránh lỗi parse dữ liệu cũ.
    /// </summary>
    [Description("Ví điện tử MoMo")]
    [HiddenPaymentMethod]
    MOMO = 2,

    [Description("Ví điện tử ZaloPay")]
    ZALOPAY = 3
}
