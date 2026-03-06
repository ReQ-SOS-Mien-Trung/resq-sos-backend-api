using System.ComponentModel.DataAnnotations;

namespace RESQ.Application.UseCases.Finance.Commands.CreateDonation;

public class CreateDonationRequestDto
{
    public int FundCampaignId { get; set; }
    public string DonorName { get; set; } = string.Empty;
    
    //[Required(ErrorMessage = "Email là bắt buộc.")]
    //[EmailAddress(ErrorMessage = "Định dạng email không hợp lệ.")]
    public string DonorEmail { get; init; } = string.Empty;
    
    public decimal Amount { get; set; }
    public string? Note { get; set; }

    /// <summary>
    /// Determines if the donor wants to remain anonymous in public lists.
    /// Default is false.
    /// </summary>
    public bool IsPrivate { get; set; } = false;
}
