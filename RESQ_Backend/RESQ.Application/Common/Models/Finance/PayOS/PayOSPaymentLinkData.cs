using System.Text.Json.Serialization;

namespace RESQ.Application.Common.Models.Finance.PayOS;

public class PayOSPaymentLinkData
{
    [JsonPropertyName("bin")]
    public string? Bin { get; set; }

    [JsonPropertyName("accountNumber")]
    public string? AccountNumber { get; set; }

    [JsonPropertyName("amount")]
    public int Amount { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("orderCode")]
    public long OrderCode { get; set; }

    [JsonPropertyName("paymentLinkId")]
    public string PaymentLinkId { get; set; } = string.Empty;

    [JsonPropertyName("checkoutUrl")]
    public string CheckoutUrl { get; set; } = string.Empty;

    [JsonPropertyName("qrCode")]
    public string QrCode { get; set; } = string.Empty;
}