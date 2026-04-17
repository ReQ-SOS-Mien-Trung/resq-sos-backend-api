using RESQ.Domain.Enum.System;

namespace RESQ.Application.Services.Ai;

public static class AiProviderDefaults
{
    public const string GeminiApiUrl = "https://generativelanguage.googleapis.com/v1beta/models/{0}:generateContent?key={1}";
    public const string OpenRouterApiUrl = "https://openrouter.ai/api/v1/chat/completions";
    public const string OpenRouterModel = "openai/gpt-4o-mini";

    public static string ResolveApiUrl(AiProvider provider)
    {
        return provider switch
        {
            AiProvider.Gemini => GeminiApiUrl,
            AiProvider.OpenRouter => OpenRouterApiUrl,
            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unsupported AI provider.")
        };
    }
}
