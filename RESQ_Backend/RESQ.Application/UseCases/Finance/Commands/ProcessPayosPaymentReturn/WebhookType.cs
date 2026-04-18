using System.Text.Json.Serialization;

namespace RESQ.Application.UseCases.Finance.Commands.ProcessPayosPaymentReturn
{
    public class WebhookType
    {
        [JsonPropertyName("code")]
        public string? Code { get; init; }

        [JsonPropertyName("desc")]
        public string? Desc { get; init; }

        [JsonPropertyName("success")]
        public bool Success { get; init; }

        [JsonPropertyName("data")]
        public WebhookDataType? Data { get; init; }

        [JsonPropertyName("signature")]
        public string? Signature { get; init; }
    }
}

