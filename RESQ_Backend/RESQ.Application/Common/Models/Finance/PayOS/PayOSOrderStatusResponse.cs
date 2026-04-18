using System.Text.Json.Serialization;

namespace RESQ.Application.Common.Models.Finance.PayOS;

/// <summary>
/// Response from PayOS GET /v2/payment-requests/{paymentLinkId}
/// </summary>
public class PayOSOrderStatusResponse
{
    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("desc")]
    public string? Desc { get; set; }

    [JsonPropertyName("data")]
    public PayOSOrderStatusData? Data { get; set; }
}

public class PayOSOrderStatusData
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("orderCode")]
    public long OrderCode { get; set; }

    [JsonPropertyName("amount")]
    public int Amount { get; set; }

    [JsonPropertyName("amountPaid")]
    public int AmountPaid { get; set; }

    [JsonPropertyName("amountRemaining")]
    public int AmountRemaining { get; set; }

    /// <summary>PAID | PROCESSING | CANCELLED | EXPIRED</summary>
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("createdAt")]
    public string? CreatedAt { get; set; }

    [JsonPropertyName("transactions")]
    public List<PayOSTransactionInfo>? Transactions { get; set; }
}

public class PayOSTransactionInfo
{
    [JsonPropertyName("reference")]
    public string? Reference { get; set; }

    [JsonPropertyName("amount")]
    public int Amount { get; set; }

    [JsonPropertyName("accountNumber")]
    public string? AccountNumber { get; set; }

    [JsonPropertyName("transactionDateTime")]
    public string? TransactionDateTime { get; set; }
}
