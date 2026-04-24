namespace RESQ.Application.UseCases.Finance.Queries.GetActiveCampaignsForDonation;

public class ActiveCampaignForDonationDto
{
    public int Id { get; set; }
    public string? Code { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public decimal TargetAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal CurrentBalance { get; set; }
    public DateOnly? CampaignStartDate { get; set; }
    public DateOnly? CampaignEndDate { get; set; }
}
