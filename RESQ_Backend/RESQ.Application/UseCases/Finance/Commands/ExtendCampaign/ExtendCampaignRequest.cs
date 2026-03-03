using System.ComponentModel.DataAnnotations;

namespace RESQ.Application.UseCases.Finance.Commands.ExtendCampaign;

public class ExtendCampaignRequest
{
    [Required]
    public DateOnly NewEndDate { get; set; }
}