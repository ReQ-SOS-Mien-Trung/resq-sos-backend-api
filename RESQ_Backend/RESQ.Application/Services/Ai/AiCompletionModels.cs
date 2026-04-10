using System.Text.Json;
using RESQ.Domain.Enum.System;

namespace RESQ.Application.Services.Ai;

public sealed class AiCompletionRequest
{
    public AiProvider Provider { get; init; } = AiProvider.Gemini;
    public string Model { get; init; } = string.Empty;
    public string ApiUrl { get; init; } = string.Empty;
    public string ApiKey { get; init; } = string.Empty;
    public string? SystemPrompt { get; init; }
    public double Temperature { get; init; }
    public int MaxTokens { get; init; }
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);
    public IReadOnlyList<AiChatMessage> Messages { get; init; } = [];
    public IReadOnlyList<AiToolDefinition> Tools { get; init; } = [];
}

public sealed class AiCompletionResponse
{
    public string? Text { get; init; }
    public IReadOnlyList<AiToolCall> ToolCalls { get; init; } = [];
    public string? FinishReason { get; init; }
    public string? BlockReason { get; init; }
    public int? HttpStatusCode { get; init; }
    public string? ErrorBody { get; init; }
    public string? RawResponse { get; init; }
    public long LatencyMs { get; init; }
}

public enum AiChatRole
{
    User = 1,
    Assistant = 2,
    Tool = 3
}

public sealed class AiChatMessage
{
    public AiChatRole Role { get; init; }
    public string? Content { get; init; }
    public IReadOnlyList<AiToolCall> ToolCalls { get; init; } = [];
    public string? ToolCallId { get; init; }
    public string? ToolName { get; init; }
    public JsonElement? ToolResult { get; init; }

    public static AiChatMessage User(string content) => new()
    {
        Role = AiChatRole.User,
        Content = content
    };

    public static AiChatMessage Assistant(string? content, IReadOnlyList<AiToolCall>? toolCalls = null) => new()
    {
        Role = AiChatRole.Assistant,
        Content = content,
        ToolCalls = toolCalls ?? []
    };

    public static AiChatMessage Tool(string toolCallId, string toolName, JsonElement toolResult) => new()
    {
        Role = AiChatRole.Tool,
        ToolCallId = toolCallId,
        ToolName = toolName,
        ToolResult = toolResult
    };
}

public sealed class AiToolCall
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public JsonElement Arguments { get; init; }
    public JsonElement? NativeFunctionCallPart { get; init; }
}

public sealed class AiToolDefinition
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public JsonElement Parameters { get; init; }
}

public sealed record AiPromptExecutionFallback(
    string GeminiModel,
    string GeminiApiUrl,
    double Temperature,
    int MaxTokens,
    string? OpenRouterModel = null,
    string? OpenRouterApiUrl = null);

public sealed record AiPromptExecutionSettings(
    AiProvider Provider,
    string Model,
    string ApiUrl,
    string ApiKey,
    double Temperature,
    int MaxTokens);