using System.ComponentModel.DataAnnotations;

namespace RESQ.Application.UseCases.Finance.Commands.CreateCampaign;

public class CreateCampaignRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;
    
    [Required]
    public string Region { get; set; } = string.Empty;
    
    [Required]
    public DateOnly CampaignStartDate { get; set; }
    
    [Required]
    public DateOnly CampaignEndDate { get; set; }
    
    [Required]
    public decimal TargetAmount { get; set; }
}
