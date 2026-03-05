namespace RESQ.Application.UseCases.Finance.Queries.GetDonations;

public class GetDonationsResponseDto
{
    public int Id { get; set; }
    public int? FundCampaignId { get; set; }
    public string FundCampaignName { get; set; } = string.Empty;
    public string DonorName { get; set; } = string.Empty;
    public string? DonorEmail { get; set; }
    public decimal Amount { get; set; }
    public string? Note { get; set; }
    public DateTime? CreatedAt { get; set; }
    public bool IsPrivate { get; set; }
}