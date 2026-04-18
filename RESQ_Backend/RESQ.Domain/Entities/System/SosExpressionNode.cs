using System.Text.Json;
using System.Text.Json.Serialization;

namespace RESQ.Domain.Entities.System;

[JsonConverter(typeof(SosExpressionNodeJsonConverter))]
public class SosExpressionNode
{
    public string? Var { get; set; }
    public string? Op { get; set; }
    public SosExpressionNode? Left { get; set; }
    public SosExpressionNode? Right { get; set; }
    public SosExpressionNode? UnaryValue { get; set; }
    public double? ConstantValue { get; set; }

    public static SosExpressionNode VarRef(string name) => new() { Var = name };
    public static SosExpressionNode Constant(double value) => new() { ConstantValue = value };
    public static SosExpressionNode Binary(string op, SosExpressionNode left, SosExpressionNode right) =>
        new() { Op = op, Left = left, Right = right };
    public static SosExpressionNode Unary(string op, SosExpressionNode value) =>
        new() { Op = op, UnaryValue = value };
}

public class SosExpressionNodeJsonConverter : JsonConverter<SosExpressionNode>
{
    public override SosExpressionNode? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expression node phải là object.");
        }

        var node = new SosExpressionNode();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return node;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Property expression không hợp lệ.");
            }

            var propertyName = reader.GetString();
            reader.Read();

            switch (propertyName)
            {
                case "var":
                    node.Var = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
                    break;

                case "op":
                    node.Op = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
                    break;

                case "left":
                    node.Left = JsonSerializer.Deserialize<SosExpressionNode>(ref reader, options);
                    break;

                case "right":
                    node.Right = JsonSerializer.Deserialize<SosExpressionNode>(ref reader, options);
                    break;

                case "value":
                    if (reader.TokenType == JsonTokenType.Number)
                    {
                        node.ConstantValue = reader.GetDouble();
                    }
                    else if (reader.TokenType == JsonTokenType.StartObject)
                    {
                        node.UnaryValue = JsonSerializer.Deserialize<SosExpressionNode>(ref reader, options);
                    }
                    else if (reader.TokenType == JsonTokenType.Null)
                    {
                        node.UnaryValue = null;
                    }
                    else
                    {
                        throw new JsonException("value của expression phải là number hoặc object.");
                    }

                    break;

                default:
                    using (JsonDocument.ParseValue(ref reader))
                    {
                    }

                    break;
            }
        }

        throw new JsonException("JSON expression node chưa kết thúc hợp lệ.");
    }

    public override void Write(Utf8JsonWriter writer, SosExpressionNode value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        if (!string.IsNullOrWhiteSpace(value.Var))
        {
            writer.WriteString("var", value.Var);
            writer.WriteEndObject();
            return;
        }

        if (string.IsNullOrWhiteSpace(value.Op))
        {
            writer.WriteNumber("value", value.ConstantValue ?? 0d);
            writer.WriteEndObject();
            return;
        }

        writer.WriteString("op", value.Op);

        if (value.Left is not null)
        {
            writer.WritePropertyName("left");
            JsonSerializer.Serialize(writer, value.Left, options);
        }

        if (value.Right is not null)
        {
            writer.WritePropertyName("right");
            JsonSerializer.Serialize(writer, value.Right, options);
        }

        if (value.UnaryValue is not null)
        {
            writer.WritePropertyName("value");
            JsonSerializer.Serialize(writer, value.UnaryValue, options);
        }
        else if (value.ConstantValue.HasValue)
        {
            writer.WriteNumber("value", value.ConstantValue.Value);
        }

        writer.WriteEndObject();
    }
}
