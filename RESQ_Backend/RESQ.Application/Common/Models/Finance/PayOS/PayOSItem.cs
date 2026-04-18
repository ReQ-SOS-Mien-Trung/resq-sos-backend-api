using System.Text.Json.Serialization;

namespace RESQ.Application.Common.Models.Finance.PayOS;

public class PayOSItem
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }

    [JsonPropertyName("price")]
    public int Price { get; set; }
}
