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

            var toolCalls = parts
                .Where(part => part.FunctionCall != null)
                .Select((part, index) => new AiToolCall
                {
                    Id = $"gemini-call-{index + 1}-{Guid.NewGuid():N}",
                    Name = part.FunctionCall!.Name,
                    Arguments = part.FunctionCall.Args
                })
                .ToList();

            return new AiCompletionResponse
            {
                Text = string.Concat(parts.Where(part => part.Text != null).Select(part => part.Text)),
                ToolCalls = toolCalls,
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

    private static object BuildRequest(AiCompletionRequest request)
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
                            new GeminiPart
                            {
                                Text = string.IsNullOrWhiteSpace(request.SystemPrompt)
                                    ? request.Messages[0].Content
                                    : $"{request.SystemPrompt}\n\n{request.Messages[0].Content}"
                            }
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
                    Parts = [new GeminiPart { Text = request.SystemPrompt }]
                },
            Contents = request.Messages.Select(MapMessage).ToList(),
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

    private static GeminiContent MapMessage(AiChatMessage message)
    {
        return message.Role switch
        {
            AiChatRole.User => new GeminiContent
            {
                Role = "user",
                Parts = [new GeminiPart { Text = message.Content ?? string.Empty }]
            },
            AiChatRole.Assistant => new GeminiContent
            {
                Role = "model",
                Parts = BuildAssistantParts(message)
            },
            AiChatRole.Tool => new GeminiContent
            {
                Role = "user",
                Parts =
                [
                    new GeminiPart
                    {
                        FunctionResponse = new GeminiFunctionResponse
                        {
                            Name = message.ToolName ?? string.Empty,
                            Response = message.ToolResult ?? JsonSerializer.SerializeToElement(new { })
                        }
                    }
                ]
            },
            _ => throw new NotSupportedException($"Unsupported Gemini role: {message.Role}")
        };
    }

    private static List<GeminiPart> BuildAssistantParts(AiChatMessage message)
    {
        var parts = new List<GeminiPart>();
        if (!string.IsNullOrWhiteSpace(message.Content))
        {
            parts.Add(new GeminiPart { Text = message.Content });
        }

        parts.AddRange(message.ToolCalls.Select(call => new GeminiPart
        {
            FunctionCall = new GeminiFunctionCall
            {
                Name = call.Name,
                Args = call.Arguments
            }
        }));

        return parts;
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
        public List<GeminiPart> Parts { get; set; } = [];
    }

    private sealed class GeminiContent
    {
        [JsonPropertyName("role")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Role { get; set; }

        [JsonPropertyName("parts")]
        public List<GeminiPart> Parts { get; set; } = [];
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