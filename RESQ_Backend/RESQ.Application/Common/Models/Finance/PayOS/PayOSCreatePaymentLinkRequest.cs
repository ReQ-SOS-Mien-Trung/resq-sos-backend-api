using System.Text.Json.Serialization;

namespace RESQ.Application.Common.Models.Finance.PayOS;

public class PayOSCreatePaymentLinkRequest
{
    [JsonPropertyName("orderCode")]
    public long OrderCode { get; set; }

    [JsonPropertyName("amount")]
    public int Amount { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("buyerName")]
    public string? BuyerName { get; set; }

    [JsonPropertyName("buyerEmail")]
    public string? BuyerEmail { get; set; }

    [JsonPropertyName("buyerPhone")]
    public string? BuyerPhone { get; set; }

    [JsonPropertyName("cancelUrl")]
    public string CancelUrl { get; set; } = string.Empty;

    [JsonPropertyName("returnUrl")]
    public string ReturnUrl { get; set; } = string.Empty;

    [JsonPropertyName("signature")]
    public string Signature { get; set; } = string.Empty;

    [JsonPropertyName("items")]
    public List<PayOSItem>? Items { get; set; }

    [JsonPropertyName("expiredAt")]
    public long? ExpiredAt { get; set; }
}