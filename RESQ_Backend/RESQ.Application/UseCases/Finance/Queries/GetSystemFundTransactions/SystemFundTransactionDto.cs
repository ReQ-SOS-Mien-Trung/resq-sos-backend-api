namespace RESQ.Application.UseCases.Finance.Queries.GetSystemFundTransactions;

/// <summary>
/// DTO lịch sử giao dịch quỹ hệ thống.
/// </summary>
public class SystemFundTransactionDto
{
    public int Id { get; set; }
    public int SystemFundId { get; set; }

    /// <summary>Loại giao dịch: LiquidationRevenue, AllocationToDepot, DepotClosureFundReturn.</summary>
    public string TransactionType { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    /// <summary>Loại tham chiếu: DepotClosure, DepotFundAllocation, v.v.</summary>
    public string? ReferenceType { get; set; }

    public int? ReferenceId { get; set; }
    public string? Note { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}
