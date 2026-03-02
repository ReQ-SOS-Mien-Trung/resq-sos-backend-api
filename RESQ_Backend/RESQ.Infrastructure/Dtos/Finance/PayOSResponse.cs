using System.Text.Json.Serialization;

namespace RESQ.Infrastructure.Dtos.Finance;

internal class PayOSResponse<T>
{
    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("desc")]
    public string? Desc { get; set; }

    [JsonPropertyName("data")]
    public T? Data { get; set; }
}
