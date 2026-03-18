using System.ComponentModel.DataAnnotations;

namespace RESQ.Application.UseCases.Finance.Commands.ApproveFundingRequest;

public class ApproveFundingRequestRequest
{
    /// <summary>Campaign mà Admin chọn để rút tiền.</summary>
    [Required]
    public int CampaignId { get; set; }
}
