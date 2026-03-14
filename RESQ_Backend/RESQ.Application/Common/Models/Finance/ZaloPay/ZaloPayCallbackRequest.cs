using System.Text.Json.Serialization;

namespace RESQ.Application.Common.Models.Finance.ZaloPay;

public class ZaloPayCallbackRequest
{
    [JsonPropertyName("data")]
    public string Data { get; set; } = string.Empty;

    [JsonPropertyName("mac")]
    public string Mac { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public int Type { get; set; }
}