using RESQ.Domain.Enum.Finance;

namespace RESQ.Application.UseCases.Finance.Commands.CreateDonation
{
    public class CreateDonationResponse
    {
        public int DonationId { get; set; }
        public string CheckoutUrl { get; set; } = string.Empty;
        public string QrCode { get; set; } = string.Empty;
        public string PaymentMethod { get; set; } = string.Empty;
    }
}
