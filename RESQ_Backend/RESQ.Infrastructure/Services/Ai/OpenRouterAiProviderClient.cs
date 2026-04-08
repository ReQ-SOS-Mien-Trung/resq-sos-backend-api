using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using RESQ.Application.Services.Ai;
using RESQ.Domain.Enum.System;

namespace RESQ.Infrastructure.Services.Ai;

public class OpenRouterAiProviderClient(
    IHttpClientFactory httpClientFactory,
    ILogger<OpenRouterAiProviderClient> logger) : IAiProviderClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly ILogger<OpenRouterAiProviderClient> _logger = logger;

    public AiProvider Provider => AiProvider.OpenRouter;

    public async Task<AiCompletionResponse> CompleteAsync(
        AiCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ApiKey))
        {
            throw new InvalidOperationException("OpenRouter API key is not configured.");
        }

        var stopwatch = Stopwatch.StartNew();
        var client = _httpClientFactory.CreateClient();
        client.Timeout = request.Timeout;

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, request.ApiUrl);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", request.ApiKey);
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var payload = new OpenRouterRequest
        {
            Model = request.Model,
            Temperature = request.Temperature,
            MaxTokens = request.MaxTokens,
            Messages = BuildMessages(request),
            Tools = request.Tools.Count == 0
                ? null
                : request.Tools.Select(tool => new OpenRouterToolDefinition
                {
                    Type = "function",
                    Function = new OpenRouterFunctionDefinition
                    {
                        Name = tool.Name,
                        Description = tool.Description,
                        Parameters = tool.Parameters
                    }
                }).ToList(),
            ToolChoice = request.Tools.Count == 0 ? null : "auto"
        };

        httpRequest.Content = new StringContent(
            JsonSerializer.Serialize(payload, SerializerOptions),
            Encoding.UTF8,
            "application/json");

        try
        {
            using var response = await client.SendAsync(httpRequest, cancellationToken);
            var raw = await response.Content.ReadAsStringAsync(cancellationToken);
            stopwatch.Stop();

            if (!response.IsSuccessStatusCode)
            {
                return new AiCompletionResponse
                {
                    HttpStatusCode = (int)response.StatusCode,
                    ErrorBody = raw,
                    RawResponse = raw,
                    LatencyMs = stopwatch.ElapsedMilliseconds
                };
            }

            var parsed = JsonSerializer.Deserialize<OpenRouterResponse>(raw, SerializerOptions);
            var choice = parsed?.Choices?.FirstOrDefault();
            var message = choice?.Message;
            var toolCalls = message?.ToolCalls?.Select(toolCall => new AiToolCall
            {
                Id = string.IsNullOrWhiteSpace(toolCall.Id) ? $"openrouter-call-{Guid.NewGuid():N}" : toolCall.Id,
                Name = toolCall.Function?.Name ?? string.Empty,
                Arguments = ParseArguments(toolCall.Function?.Arguments)
            }).ToList() ?? [];

            return new AiCompletionResponse
            {
                Text = ExtractContent(message?.Content),
                ToolCalls = toolCalls,
                FinishReason = choice?.FinishReason,
                HttpStatusCode = (int)response.StatusCode,
                RawResponse = raw,
                LatencyMs = stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "OpenRouter request failed for model {Model}", request.Model);
            throw;
        }
    }

    private static List<OpenRouterMessage> BuildMessages(AiCompletionRequest request)
    {
        var messages = new List<OpenRouterMessage>();

        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            messages.Add(new OpenRouterMessage
            {
                Role = "system",
                Content = request.SystemPrompt
            });
        }

        messages.AddRange(request.Messages.Select(MapMessage));
        return messages;
    }

    private static OpenRouterMessage MapMessage(AiChatMessage message)
    {
        return message.Role switch
        {
            AiChatRole.User => new OpenRouterMessage
            {
                Role = "user",
                Content = message.Content ?? string.Empty
            },
            AiChatRole.Assistant => new OpenRouterMessage
            {
                Role = "assistant",
                Content = message.Content,
                ToolCalls = message.ToolCalls.Count == 0
                    ? null
                    : message.ToolCalls.Select(toolCall => new OpenRouterToolCall
                    {
                        Id = string.IsNullOrWhiteSpace(toolCall.Id) ? $"call_{Guid.NewGuid():N}" : toolCall.Id,
                        Type = "function",
                        Function = new OpenRouterFunctionCall
                        {
                            Name = toolCall.Name,
                            Arguments = toolCall.Arguments.ValueKind == JsonValueKind.Undefined
                                ? "{}"
                                : toolCall.Arguments.GetRawText()
                        }
                    }).ToList()
            },
            AiChatRole.Tool => new OpenRouterMessage
            {
                Role = "tool",
                ToolCallId = message.ToolCallId,
                Name = message.ToolName,
                Content = message.ToolResult?.GetRawText() ?? "{}"
            },
            _ => throw new NotSupportedException($"Unsupported OpenRouter role: {message.Role}")
        };
    }

    private static JsonElement ParseArguments(string? rawArguments)
    {
        if (string.IsNullOrWhiteSpace(rawArguments))
        {
            return JsonSerializer.SerializeToElement(new { });
        }

        try
        {
            using var document = JsonDocument.Parse(rawArguments);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return JsonSerializer.SerializeToElement(new { raw = rawArguments });
        }
    }

    private static string? ExtractContent(JsonElement? content)
    {
        if (!content.HasValue)
        {
            return null;
        }

        return content.Value.ValueKind switch
        {
            JsonValueKind.String => content.Value.GetString(),
            JsonValueKind.Array => string.Concat(content.Value.EnumerateArray()
                .Select(item => item.TryGetProperty("text", out var text) ? text.GetString() : item.GetRawText())),
            JsonValueKind.Null => null,
            _ => content.Value.GetRawText()
        };
    }

    private sealed class OpenRouterRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("messages")]
        public List<OpenRouterMessage> Messages { get; set; } = [];

        [JsonPropertyName("temperature")]
        public double Temperature { get; set; }

        [JsonPropertyName("max_tokens")]
        public int MaxTokens { get; set; }

        [JsonPropertyName("tools")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<OpenRouterToolDefinition>? Tools { get; set; }

        [JsonPropertyName("tool_choice")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ToolChoice { get; set; }
    }

    private sealed class OpenRouterMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public object? Content { get; set; }

        [JsonPropertyName("tool_calls")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<OpenRouterToolCall>? ToolCalls { get; set; }

        [JsonPropertyName("tool_call_id")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ToolCallId { get; set; }

        [JsonPropertyName("name")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Name { get; set; }
    }

    private sealed class OpenRouterToolDefinition
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("function")]
        public OpenRouterFunctionDefinition Function { get; set; } = new();
    }

    private sealed class OpenRouterFunctionDefinition
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("parameters")]
        public JsonElement Parameters { get; set; }
    }

    private sealed class OpenRouterToolCall
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("function")]
        public OpenRouterFunctionCall? Function { get; set; }
    }

    private sealed class OpenRouterFunctionCall
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("arguments")]
        public string Arguments { get; set; } = "{}";
    }

    private sealed class OpenRouterResponse
    {
        [JsonPropertyName("choices")]
        public List<OpenRouterChoice>? Choices { get; set; }
    }

    private sealed class OpenRouterChoice
    {
        [JsonPropertyName("message")]
        public OpenRouterResponseMessage? Message { get; set; }

        [JsonPropertyName("finish_reason")]
        public string? FinishReason { get; set; }
    }

    private sealed class OpenRouterResponseMessage
    {
        [JsonPropertyName("content")]
        public JsonElement? Content { get; set; }

        [JsonPropertyName("tool_calls")]
        public List<OpenRouterToolCall>? ToolCalls { get; set; }
    }
}