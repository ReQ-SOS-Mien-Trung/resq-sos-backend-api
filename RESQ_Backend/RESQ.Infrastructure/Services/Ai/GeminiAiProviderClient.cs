using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using RESQ.Application.Services.Ai;
using RESQ.Domain.Enum.System;

namespace RESQ.Infrastructure.Services.Ai;

public class GeminiAiProviderClient(
    IHttpClientFactory httpClientFactory,
    ILogger<GeminiAiProviderClient> logger) : IAiProviderClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly ILogger<GeminiAiProviderClient> _logger = logger;

    public AiProvider Provider => AiProvider.Gemini;

    public async Task<AiCompletionResponse> CompleteAsync(
        AiCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ApiKey))
        {
            throw new InvalidOperationException("Gemini API key is not configured.");
        }

        var stopwatch = Stopwatch.StartNew();
        var client = _httpClientFactory.CreateClient();
        client.Timeout = request.Timeout;

        var url = string.Format(request.ApiUrl, request.Model, request.ApiKey);
    var payload = BuildRequest(request);

        try
        {
            using var response = await client.PostAsJsonAsync(url, payload, SerializerOptions, cancellationToken);
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

            var parsed = JsonSerializer.Deserialize<GeminiResponseEnvelope>(raw, SerializerOptions);
            var candidate = parsed?.Candidates?.FirstOrDefault();
            var parts = candidate?.Content?.Parts ?? [];

            return new AiCompletionResponse
            {
                Text = string.Concat(parts.Select(GetTextPart).Where(text => text != null)),
                ToolCalls = ParseToolCalls(parts),
                FinishReason = candidate?.FinishReason,
                BlockReason = parsed?.PromptFeedback?.BlockReason,
                HttpStatusCode = (int)response.StatusCode,
                RawResponse = raw,
                LatencyMs = stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Gemini request failed for model {Model}", request.Model);
            throw;
        }
    }

    private GeminiRequest BuildRequest(AiCompletionRequest request)
    {
        if (request.Tools.Count == 0 && request.Messages.Count == 1 && request.Messages[0].Role == AiChatRole.User)
        {
            return new GeminiRequest
            {
                Contents =
                [
                    new GeminiContent
                    {
                        Parts =
                        [
                            CreateTextPart(
                                string.IsNullOrWhiteSpace(request.SystemPrompt)
                                    ? request.Messages[0].Content ?? string.Empty
                                    : $"{request.SystemPrompt}\n\n{request.Messages[0].Content}")
                        ]
                    }
                ],
                GenerationConfig = new GeminiGenerationConfig
                {
                    Temperature = request.Temperature,
                    MaxOutputTokens = request.MaxTokens
                }
            };
        }

        return new GeminiRequest
        {
            SystemInstruction = string.IsNullOrWhiteSpace(request.SystemPrompt)
                ? null
                : new GeminiSystemInstruction
                {
                    Parts = [CreateTextPart(request.SystemPrompt)]
                },
            Contents = request.Messages.Select((message, index) => MapMessage(message, index)).ToList(),
            Tools = request.Tools.Count == 0
                ? null
                :
                [
                    new GeminiTool
                    {
                        FunctionDeclarations = request.Tools.Select(tool => new GeminiFunctionDeclaration
                        {
                            Name = tool.Name,
                            Description = tool.Description,
                            Parameters = tool.Parameters
                        }).ToList()
                    }
                ],
            GenerationConfig = new GeminiGenerationConfig
            {
                Temperature = request.Temperature,
                MaxOutputTokens = request.MaxTokens
            }
        };
    }

    private GeminiContent MapMessage(AiChatMessage message, int messageIndex)
    {
        return message.Role switch
        {
            AiChatRole.User => new GeminiContent
            {
                Role = "user",
                Parts = [CreateTextPart(message.Content ?? string.Empty)]
            },
            AiChatRole.Assistant => new GeminiContent
            {
                Role = "model",
                Parts = BuildAssistantParts(message, messageIndex)
            },
            AiChatRole.Tool => new GeminiContent
            {
                Role = "user",
                Parts =
                [
                    CreateFunctionResponsePart(
                        message.ToolName ?? string.Empty,
                        message.ToolResult ?? CreateEmptyObject())
                ]
            },
            _ => throw new NotSupportedException($"Unsupported Gemini role: {message.Role}")
        };
    }

    private List<JsonElement> BuildAssistantParts(AiChatMessage message, int messageIndex)
    {
        var parts = new List<JsonElement>();
        if (!string.IsNullOrWhiteSpace(message.Content))
        {
            parts.Add(CreateTextPart(message.Content));
        }

        for (var toolCallIndex = 0; toolCallIndex < message.ToolCalls.Count; toolCallIndex++)
        {
            var toolCall = message.ToolCalls[toolCallIndex];
            if (toolCall.NativeFunctionCallPart is { ValueKind: JsonValueKind.Object } nativePart)
            {
                parts.Add(nativePart.Clone());
                continue;
            }

            _logger.LogWarning(
                "Gemini replay is rebuilding functionCall at contents[{MessageIndex}].parts[{PartIndex}] for tool {ToolName} ({ToolCallId}) because the provider-native functionCall part payload was not preserved. Gemini may reject the follow-up request if a thought signature was required.",
                messageIndex,
                parts.Count,
                toolCall.Name,
                toolCall.Id);

            parts.Add(CreateFunctionCallPart(toolCall.Name, toolCall.Arguments));
        }

        return parts;
    }

    private static List<AiToolCall> ParseToolCalls(IReadOnlyList<JsonElement> parts)
    {
        var toolCalls = new List<AiToolCall>();

        for (var index = 0; index < parts.Count; index++)
        {
            if (!TryGetFunctionCall(parts[index], out var name, out var arguments))
            {
                continue;
            }

            toolCalls.Add(new AiToolCall
            {
                Id = $"gemini-call-{toolCalls.Count + 1}-{Guid.NewGuid():N}",
                Name = name,
                Arguments = arguments,
                NativeFunctionCallPart = parts[index].Clone()
            });
        }

        return toolCalls;
    }

    private static bool TryGetFunctionCall(JsonElement part, out string name, out JsonElement arguments)
    {
        name = string.Empty;
        arguments = CreateEmptyObject();

        if (part.ValueKind != JsonValueKind.Object
            || !part.TryGetProperty("functionCall", out var functionCall)
            || functionCall.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!functionCall.TryGetProperty("name", out var nameElement))
        {
            return false;
        }

        name = nameElement.GetString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        if (functionCall.TryGetProperty("args", out var argsElement))
        {
            arguments = argsElement.Clone();
        }

        return true;
    }

    private static string? GetTextPart(JsonElement part)
    {
        return part.ValueKind == JsonValueKind.Object
            && part.TryGetProperty("text", out var textElement)
            ? textElement.GetString()
            : null;
    }

    private static JsonElement CreateTextPart(string? text)
    {
        return JsonSerializer.SerializeToElement(new GeminiPart { Text = text }, SerializerOptions);
    }

    private static JsonElement CreateFunctionCallPart(string name, JsonElement arguments)
    {
        return JsonSerializer.SerializeToElement(new GeminiPart
        {
            FunctionCall = new GeminiFunctionCall
            {
                Name = name,
                Args = arguments.ValueKind == JsonValueKind.Undefined ? CreateEmptyObject() : arguments
            }
        }, SerializerOptions);
    }

    private static JsonElement CreateFunctionResponsePart(string name, JsonElement response)
    {
        return JsonSerializer.SerializeToElement(new GeminiPart
        {
            FunctionResponse = new GeminiFunctionResponse
            {
                Name = name,
                Response = response.ValueKind == JsonValueKind.Undefined ? CreateEmptyObject() : response
            }
        }, SerializerOptions);
    }

    private static JsonElement CreateEmptyObject()
    {
        return JsonSerializer.SerializeToElement(new { });
    }

    private sealed class GeminiRequest
    {
        [JsonPropertyName("system_instruction")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public GeminiSystemInstruction? SystemInstruction { get; set; }

        [JsonPropertyName("contents")]
        public List<GeminiContent> Contents { get; set; } = [];

        [JsonPropertyName("tools")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<GeminiTool>? Tools { get; set; }

        [JsonPropertyName("generationConfig")]
        public GeminiGenerationConfig? GenerationConfig { get; set; }
    }

    private sealed class GeminiSystemInstruction
    {
        [JsonPropertyName("parts")]
        public List<JsonElement> Parts { get; set; } = [];
    }

    private sealed class GeminiContent
    {
        [JsonPropertyName("role")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Role { get; set; }

        [JsonPropertyName("parts")]
        public List<JsonElement> Parts { get; set; } = [];
    }

    private sealed class GeminiPart
    {
        [JsonPropertyName("text")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Text { get; set; }

        [JsonPropertyName("functionCall")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public GeminiFunctionCall? FunctionCall { get; set; }

        [JsonPropertyName("functionResponse")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public GeminiFunctionResponse? FunctionResponse { get; set; }
    }

    private sealed class GeminiFunctionCall
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("args")]
        public JsonElement Args { get; set; }
    }

    private sealed class GeminiFunctionResponse
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("response")]
        public JsonElement Response { get; set; }
    }

    private sealed class GeminiTool
    {
        [JsonPropertyName("functionDeclarations")]
        public List<GeminiFunctionDeclaration> FunctionDeclarations { get; set; } = [];
    }

    private sealed class GeminiFunctionDeclaration
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("parameters")]
        public JsonElement Parameters { get; set; }
    }

    private sealed class GeminiGenerationConfig
    {
        [JsonPropertyName("temperature")]
        public double Temperature { get; set; }

        [JsonPropertyName("maxOutputTokens")]
        public int MaxOutputTokens { get; set; }
    }

    private sealed class GeminiResponseEnvelope
    {
        [JsonPropertyName("candidates")]
        public List<GeminiCandidate>? Candidates { get; set; }

        [JsonPropertyName("promptFeedback")]
        public GeminiPromptFeedback? PromptFeedback { get; set; }
    }

    private sealed class GeminiPromptFeedback
    {
        [JsonPropertyName("blockReason")]
        public string? BlockReason { get; set; }
    }

    private sealed class GeminiCandidate
    {
        [JsonPropertyName("content")]
        public GeminiContent? Content { get; set; }

        [JsonPropertyName("finishReason")]
        public string? FinishReason { get; set; }
    }
}