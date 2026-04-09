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
    
    /// <summary>Kho tự ứng tiền nhập hàng khi quỹ không đủ (balance → âm).</summary>
    SelfAdvance,
    
    /// <summary>Trừ nợ tự động khi kho nhận tiền mới (từ phần đã tự ứng trước đó).</summary>
    DebtRepayment,

    /// <summary>Tiền thu được từ thanh lý tài sản khi đóng kho.</summary>
    LiquidationRevenue,

    /// <summary>Hoàn tiền quỹ kho về quỹ hệ thống khi đóng kho.</summary>
    ClosureFundReturn
}
