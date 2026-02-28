using System.ComponentModel.DataAnnotations;

namespace RESQ.Application.UseCases.Finance.Commands.CreateDonation;

public class CreateDonationRequestDto
{
    public int FundCampaignId { get; set; }
    public string DonorName { get; set; } = string.Empty;
    [EmailAddress]
    public string DonorEmail { get; init; } = string.Empty;
    public decimal Amount { get; set; }
    public string? Note { get; set; }
}
