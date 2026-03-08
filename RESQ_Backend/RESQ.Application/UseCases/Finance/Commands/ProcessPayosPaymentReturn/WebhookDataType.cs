using System.Text.Json.Serialization;
using System.Collections.Generic;

namespace RESQ.Application.UseCases.Finance.Commands.ProcessPayosPaymentReturn
{
    // Corresponds strictly to the "data" object in PayOS JSON
    public class WebhookDataType
    {
        [JsonPropertyName("orderCode")]
        public long OrderCode { get; init; }

        [JsonPropertyName("amount")]
        public int Amount { get; init; }

        [JsonPropertyName("description")]
        public string? Description { get; init; }

        [JsonPropertyName("accountNumber")]
        public string? AccountNumber { get; init; }

        [JsonPropertyName("reference")]
        public string? Reference { get; init; }

        [JsonPropertyName("transactionDateTime")]
        public string? TransactionDateTime { get; init; }

        [JsonPropertyName("currency")]
        public string? Currency { get; init; }

        [JsonPropertyName("paymentLinkId")]
        public string? PaymentLinkId { get; init; }

        [JsonPropertyName("counterAccountBankId")]
        public string? CounterAccountBankId { get; init; }

        [JsonPropertyName("counterAccountBankName")]
        public string? CounterAccountBankName { get; init; }

        [JsonPropertyName("counterAccountName")]
        public string? CounterAccountName { get; init; }

        [JsonPropertyName("counterAccountNumber")]
        public string? CounterAccountNumber { get; init; }

        [JsonPropertyName("virtualAccountName")]
        public string? VirtualAccountName { get; init; }

        [JsonPropertyName("virtualAccountNumber")]
        public string? VirtualAccountNumber { get; init; }
        
        // Captures any other fields inside "data"
        [JsonExtensionData]
        public Dictionary<string, object>? ExtraData { get; set; }
    }
}

