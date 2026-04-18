using System.ComponentModel.DataAnnotations;

namespace RESQ.Application.UseCases.Finance.Commands.RejectFundingRequest;

public class RejectFundingRequestRequest
{
    [Required]
    public string Reason { get; set; } = string.Empty;
}
