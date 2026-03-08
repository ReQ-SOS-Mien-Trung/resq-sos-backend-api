using System.Text.Json.Serialization;

namespace RESQ.Application.Common.Models.Finance.Momo;

public class MomoOneTimePaymentRequest
{
    [JsonPropertyName("partnerCode")]
    public string PartnerCode { get; set; } = string.Empty;

    [JsonPropertyName("partnerName")]
    public string PartnerName { get; set; } = "RESQ";

    [JsonPropertyName("storeId")]
    public string StoreId { get; set; } = "RESQ";

    [JsonPropertyName("requestId")]
    public string RequestId { get; set; } = string.Empty;

    [JsonPropertyName("amount")]
    public long Amount { get; set; }

    [JsonPropertyName("orderId")]
    public string OrderId { get; set; } = string.Empty;

    [JsonPropertyName("orderInfo")]
    public string OrderInfo { get; set; } = string.Empty;

    [JsonPropertyName("redirectUrl")]
    public string RedirectUrl { get; set; } = string.Empty;

    [JsonPropertyName("ipnUrl")]
    public string IpnUrl { get; set; } = string.Empty;

    [JsonPropertyName("requestType")]
    public string RequestType { get; set; } = "captureWallet";

    [JsonPropertyName("extraData")]
    public string ExtraData { get; set; } = string.Empty;

    [JsonPropertyName("lang")]
    public string Lang { get; set; } = "vi";

    [JsonPropertyName("signature")]
    public string Signature { get; set; } = string.Empty;
}