using System.Text.Json.Serialization;

namespace RESQ.Application.Common.Models.Finance.Momo;

public class MomoIpnRequest
{
    [JsonPropertyName("partnerCode")]
    public string PartnerCode { get; set; } = string.Empty;

    [JsonPropertyName("orderId")]
    public string OrderId { get; set; } = string.Empty;

    [JsonPropertyName("requestId")]
    public string RequestId { get; set; } = string.Empty;

    [JsonPropertyName("amount")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] // Explicitly allow string reading
    public long Amount { get; set; }

    [JsonPropertyName("orderInfo")]
    public string OrderInfo { get; set; } = string.Empty;

    [JsonPropertyName("orderType")]
    public string OrderType { get; set; } = string.Empty;

    [JsonPropertyName("transId")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] // Explicitly allow string reading
    public long TransId { get; set; }

    [JsonPropertyName("resultCode")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] // Explicitly allow string reading
    public int ResultCode { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("payType")]
    public string PayType { get; set; } = string.Empty;

    [JsonPropertyName("responseTime")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] // Explicitly allow string reading
    public long ResponseTime { get; set; }

    [JsonPropertyName("extraData")]
    public string ExtraData { get; set; } = string.Empty;

    [JsonPropertyName("signature")]
    public string Signature { get; set; } = string.Empty;
}
