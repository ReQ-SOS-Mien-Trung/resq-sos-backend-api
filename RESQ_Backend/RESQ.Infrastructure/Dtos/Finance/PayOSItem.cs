using System.Text.Json.Serialization;

namespace RESQ.Infrastructure.Dtos.Finance;

internal class PayOSItem
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }

    [JsonPropertyName("price")]
    public int Price { get; set; }
}
