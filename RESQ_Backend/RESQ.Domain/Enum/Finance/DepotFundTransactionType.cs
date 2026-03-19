namespace RESQ.Domain.Enum.Finance;

/// <summary>
/// Loại giao dịch quỹ kho.
/// </summary>
public enum DepotFundTransactionType
{
    /// <summary>Cấp quỹ vào kho (Admin allocate / approve funding request).</summary>
    Allocation,
    
    /// <summary>Trừ quỹ khi nhập hàng (import-purchase).</summary>
    Deduction,
    
    /// <summary>Hoàn quỹ (trường hợp đặc biệt).</summary>
    Refund
}
