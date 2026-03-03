using RESQ.Domain.Enum.Finance;

namespace RESQ.Application.UseCases.Finance.Queries.ViewAllCampaigns;

public class CampaignListDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public decimal TargetAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateOnly? CampaignStartDate { get; set; }
    public DateOnly? CampaignEndDate { get; set; }
    public DateTime? CreatedAt { get; set; }
}
