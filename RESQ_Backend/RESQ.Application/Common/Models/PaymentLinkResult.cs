namespace RESQ.Application.Common.Models
{
    public class PaymentLinkResult
    {
        public string CheckoutUrl { get; set; } = string.Empty;
        public string PaymentLinkId { get; set; } = string.Empty;
        public string OrderCode { get; set; } = string.Empty;
        public string QrCode { get; set; } = string.Empty;
    }
}
