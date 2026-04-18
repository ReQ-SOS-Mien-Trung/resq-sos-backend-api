namespace RESQ.Application.UseCases.Finance.Queries.GetDepotFundTransactions;

/// <summary>
/// DTO lịch sử giao dịch quỹ của một kho cứu trợ.
/// </summary>
public class DepotFundTransactionDto
{
    public int Id { get; set; }
    public int DepotFundId { get; set; }

    /// <summary>Loại giao dịch: Allocation, Deduction, Refund.</summary>
    public string TransactionType { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    /// <summary>Loại tham chiếu: CampaignDisbursement, VatInvoice, v.v.</summary>
    public string? ReferenceType { get; set; }

    public int? ReferenceId { get; set; }
    public string? Note { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? ContributorName { get; set; }
    public string? ContributorPhoneNumber { get; set; }
    public decimal? ContributorTotalAdvancedAmount { get; set; }
    public decimal? ContributorTotalRepaidAmount { get; set; }
    public decimal? ContributorOutstandingAmount { get; set; }
    public decimal? ContributorRepaidPercentage { get; set; }
}
