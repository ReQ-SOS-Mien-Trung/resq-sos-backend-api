using RESQ.Domain.Enum.Finance;
using System.ComponentModel.DataAnnotations;

namespace RESQ.Application.UseCases.Finance.Commands.ChangeCampaignStatus;

public class ChangeStatusRequest
{
    [Required]
    public FundCampaignStatus NewStatus { get; set; }
}
