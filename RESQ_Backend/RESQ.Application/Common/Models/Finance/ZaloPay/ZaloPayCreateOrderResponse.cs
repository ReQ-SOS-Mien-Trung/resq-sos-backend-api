using System.Text.Json.Serialization;

namespace RESQ.Application.Common.Models.Finance.ZaloPay;

public class ZaloPayCreateOrderResponse
{[JsonPropertyName("return_code")]
    public int ReturnCode { get; set; }

    [JsonPropertyName("return_message")]
    public string ReturnMessage { get; set; } = string.Empty;[JsonPropertyName("sub_return_code")]
    public int SubReturnCode { get; set; }[JsonPropertyName("sub_return_message")]
    public string SubReturnMessage { get; set; } = string.Empty;

    [JsonPropertyName("order_url")]
    public string OrderUrl { get; set; } = string.Empty;

    [JsonPropertyName("zp_trans_token")]
    public string ZpTransToken { get; set; } = string.Empty;

    [JsonPropertyName("order_token")]
    public string OrderToken { get; set; } = string.Empty;
}
