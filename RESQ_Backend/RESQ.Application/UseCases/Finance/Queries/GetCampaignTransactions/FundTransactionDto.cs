namespace RESQ.Application.UseCases.Finance.Queries.GetCampaignTransactions;

/// <summary>
/// DTO lịch sử giao dịch tài chính của chiến dịch gây quỹ.
/// </summary>
public class FundTransactionDto
{
    public int Id { get; set; }
    public int? FundCampaignId { get; set; }
    public string? FundCampaignName { get; set; }

    /// <summary>Loại giao dịch: Donation, Allocation, Adjustment.</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Chiều giao dịch: In / Out.</summary>
    public string? Direction { get; set; }

    public decimal? Amount { get; set; }

    /// <summary>Loại tham chiếu: Donation, CampaignDisbursement, v.v.</summary>
    public string ReferenceType { get; set; } = string.Empty;

    public int? ReferenceId { get; set; }
    public string? CreatedByUserName { get; set; }
    public DateTime? CreatedAt { get; set; }
}
