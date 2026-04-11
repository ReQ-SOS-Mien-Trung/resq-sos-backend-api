namespace RESQ.Domain.Enum.Finance;

/// <summary>
/// Loại giao dịch quỹ kho.
/// </summary>
public enum DepotFundTransactionType
{
    /// <summary>Cấp quỹ vào kho (Admin allocate / approve funding request).</summary>
    Allocation,
    
    /// <summary>Trừ quỹ khi nhập hàng (import-purchase) — đủ số dư.</summary>
    Deduction,
    
    /// <summary>Hoàn quỹ (trường hợp đặc biệt).</summary>
    Refund,
    
    /// <summary>[Obsolete] Kho tự ứng tiền nhập hàng khi quỹ không đủ (balance → âm). Chỉ dùng cho dữ liệu lịch sử.</summary>
    [Obsolete("Replaced by PersonalAdvance. Kept for historical audit records.")]
    SelfAdvance,
    
    /// <summary>[Obsolete] Trừ nợ tự động khi kho nhận tiền mới. Chỉ dùng cho dữ liệu lịch sử.</summary>
    [Obsolete("Replaced by AdvanceRepayment. Kept for historical audit records.")]
    DebtRepayment,

    /// <summary>Tiền thu được từ thanh lý tài sản khi đóng kho.</summary>
    LiquidationRevenue,

    /// <summary>Hoàn tiền quỹ kho về quỹ hệ thống khi đóng kho.</summary>
    ClosureFundReturn,

    /// <summary>Ứng trước cá nhân cho kho — tăng Balance + tăng OutstandingAdvanceAmount.</summary>
    PersonalAdvance,

    /// <summary>Hoàn trả tiền ứng trước cho cá nhân — giảm Balance + giảm OutstandingAdvanceAmount.</summary>
    AdvanceRepayment
}
