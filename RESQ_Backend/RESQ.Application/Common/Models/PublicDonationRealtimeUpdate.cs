namespace RESQ.Application.Common.Models;

public sealed class PublicDonationRealtimeUpdate
{
    public int DonationId { get; init; }
    public string ReceiptCode { get; init; } = string.Empty;
    public int? FundCampaignId { get; init; }
    public string FundCampaignName { get; init; } = string.Empty;
    public string DonorName { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string? Note { get; init; }
    public DateTime? CreatedAt { get; init; }
    public DateTime? PaidAt { get; init; }
    public bool IsPrivate { get; init; }
    public string DisplayText { get; init; } = string.Empty;
    public DateTime ChangedAt { get; set; }
}
