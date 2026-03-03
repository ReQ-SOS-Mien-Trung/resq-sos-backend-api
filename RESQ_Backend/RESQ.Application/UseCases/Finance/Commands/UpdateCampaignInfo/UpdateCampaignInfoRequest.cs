using System.ComponentModel.DataAnnotations;

namespace RESQ.Application.UseCases.Finance.Commands.UpdateCampaignInfo;

public class UpdateCampaignInfoRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;
    
    [Required]
    public string Region { get; set; } = string.Empty;
}