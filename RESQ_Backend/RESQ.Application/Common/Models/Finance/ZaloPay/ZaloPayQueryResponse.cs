using System.Text.Json.Serialization;

namespace RESQ.Application.Common.Models.Finance.ZaloPay;

public class ZaloPayQueryResponse
{
    /// <summary>
    /// 1 = Paid, 2 = Rejected/Failed, 3 = Pending/Processing
    /// </summary>
    [JsonPropertyName("return_code")]
    public int ReturnCode { get; set; }

    [JsonPropertyName("return_message")]
    public string ReturnMessage { get; set; } = string.Empty;

    [JsonPropertyName("sub_return_code")]
    public int SubReturnCode { get; set; }

    [JsonPropertyName("sub_return_message")]
    public string SubReturnMessage { get; set; } = string.Empty;

    [JsonPropertyName("is_processing")]
    public bool IsProcessing { get; set; }

    [JsonPropertyName("amount")]
    public long Amount { get; set; }

    [JsonPropertyName("zp_trans_id")]
    public long ZpTransId { get; set; }

    [JsonPropertyName("server_time")]
    public long ServerTime { get; set; }

    [JsonPropertyName("channel")]
    public int Channel { get; set; }

    [JsonPropertyName("merchant_user_id")]
    public string MerchantUserId { get; set; } = string.Empty;

    [JsonPropertyName("user_fee_amount")]
    public long UserFeeAmount { get; set; }

    [JsonPropertyName("discount_amount")]
    public long DiscountAmount { get; set; }
}
