using System.Text.Json;
using System.Text.Json.Serialization;
using RESQ.Application.Common.Constants;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.Common.Json;

/// <summary>
/// Hỗ trợ bind ExternalDispositionType từ:
/// - tên enum: "Disposed"
/// - chuỗi Excel/display: "Disposed - Thanh lý / tiêu hủy"
/// - nhãn tiếng Việt: "Thanh lý / tiêu hủy"
/// </summary>
public class ExternalDispositionTypeJsonConverter : JsonConverter<ExternalDispositionType?>
{
    public override ExternalDispositionType? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        if (reader.TokenType == JsonTokenType.String)
        {
            var raw = reader.GetString()?.Trim();
            if (string.IsNullOrWhiteSpace(raw))
                return null;

            var parsed = ExternalDispositionMetadata.Parse(raw);
            if (parsed.HasValue)
                return parsed.Value;

            throw new JsonException($"Giá trị HandlingMethod '{raw}' không hợp lệ.");
        }

        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var intValue))
        {
            if (Enum.IsDefined(typeof(ExternalDispositionType), intValue))
                return (ExternalDispositionType)intValue;
        }

        throw new JsonException("Không thể parse HandlingMethod thành ExternalDispositionType.");
    }

    public override void Write(Utf8JsonWriter writer, ExternalDispositionType? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
        {
            writer.WriteStringValue(value.Value.ToString());
            return;
        }

        writer.WriteNullValue();
    }
}
