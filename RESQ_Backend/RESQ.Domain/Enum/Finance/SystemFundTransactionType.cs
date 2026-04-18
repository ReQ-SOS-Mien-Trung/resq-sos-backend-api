namespace RESQ.Domain.Enum.Finance;

/// <summary>Loại giao dịch trên quỹ hệ thống.</summary>
public enum SystemFundTransactionType
{
    /// <summary>Tiền thu được từ thanh lý tài sản khi đóng kho.</summary>
    LiquidationRevenue,

    /// <summary>Giải ngân từ quỹ hệ thống cho quỹ kho.</summary>
    AllocationToDepot,

    /// <summary>Tiền từ quỹ kho hoàn về quỹ hệ thống khi đóng kho.</summary>
    DepotClosureFundReturn
}
