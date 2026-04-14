using Microsoft.Extensions.Options;
using RESQ.Application.Services.Ai;
using RESQ.Domain.Entities.System;
using RESQ.Domain.Enum.System;
using RESQ.Infrastructure.Options;

namespace RESQ.Infrastructure.Services.Ai;

public class AiPromptExecutionSettingsResolver(
    IOptions<AiProvidersOptions> options,
    IPromptSecretProtector promptSecretProtector) : IAiPromptExecutionSettingsResolver
{
    private readonly AiProvidersOptions _options = options.Value;
    private readonly IPromptSecretProtector _promptSecretProtector = promptSecretProtector;

    public AiPromptExecutionSettings Resolve(PromptModel prompt, AiPromptExecutionFallback fallback)
    {
        var provider = prompt.Provider;
        var providerOptions = provider == AiProvider.OpenRouter
            ? _options.OpenRouter
            : _options.Gemini;

        var model = !string.IsNullOrWhiteSpace(prompt.Model)
            ? prompt.Model
            : !string.IsNullOrWhiteSpace(providerOptions.DefaultModel)
                ? providerOptions.DefaultModel
                : provider == AiProvider.OpenRouter
                    ? fallback.OpenRouterModel ?? AiProviderDefaults.OpenRouterModel
                    : fallback.GeminiModel;

        var apiUrl = !string.IsNullOrWhiteSpace(prompt.ApiUrl)
            ? prompt.ApiUrl
            : !string.IsNullOrWhiteSpace(providerOptions.ApiUrl)
                ? providerOptions.ApiUrl
                : provider == AiProvider.OpenRouter
                    ? fallback.OpenRouterApiUrl ?? AiProviderDefaults.OpenRouterApiUrl
                    : fallback.GeminiApiUrl;

        var apiKey = _promptSecretProtector.Unprotect(prompt.ApiKey)
            ?? providerOptions.ApiKey
            ?? string.Empty;

        return new AiPromptExecutionSettings(
            provider,
            model ?? string.Empty,
            apiUrl ?? string.Empty,
            apiKey,
            prompt.Temperature ?? fallback.Temperature,
            prompt.MaxTokens ?? fallback.MaxTokens);
    }
}
