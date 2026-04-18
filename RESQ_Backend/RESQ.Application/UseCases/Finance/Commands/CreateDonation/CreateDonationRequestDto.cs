namespace RESQ.Application.UseCases.Finance.Commands.CreateDonation;

public class CreateDonationRequestDto
{
    public int FundCampaignId { get; set; }
    public string DonorName { get; set; } = string.Empty;
    public string DonorEmail { get; init; } = string.Empty;
    public decimal Amount { get; set; }
    public string? Note { get; set; }
    public bool IsPrivate { get; set; } = false;
    /// <summary>Mã phương thức thanh toán ("PAYOS", "ZALOPAY").</summary>
    public string PaymentMethodCode { get; set; } = string.Empty;
}
