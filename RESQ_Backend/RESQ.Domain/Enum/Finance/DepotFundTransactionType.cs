namespace RESQ.Domain.Enum.Finance;

/// <summary>
/// Loại giao dịch quỹ kho.
/// </summary>
public enum DepotFundTransactionType
{
    /// <summary>Cấp quỹ vào kho (Admin allocate / approve funding request).</summary>
    Allocation,
    
    /// <summary>Trừ quỹ khi nhập hàng (import-purchase) - đủ số dư.</summary>
    Deduction,
    
    /// <summary>Hoàn quỹ (trường hợp đặc biệt).</summary>
    Refund,
    
    /// <summary>Tiền thu được từ thanh lý tài sản khi đóng kho.</summary>
    LiquidationRevenue,

    /// <summary>Hoàn tiền quỹ kho về quỹ hệ thống khi đóng kho.</summary>
    ClosureFundReturn,

    /// <summary>Ứng trước cá nhân cho kho - tăng Balance + tăng OutstandingAdvanceAmount.</summary>
    PersonalAdvance,

    /// <summary>Hoàn trả tiền ứng trước cho cá nhân - giảm Balance + giảm OutstandingAdvanceAmount.</summary>
    AdvanceRepayment
}
