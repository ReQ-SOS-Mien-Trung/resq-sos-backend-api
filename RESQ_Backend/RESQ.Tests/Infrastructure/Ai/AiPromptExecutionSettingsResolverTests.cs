using Microsoft.Extensions.Options;
using RESQ.Application.Services.Ai;
using RESQ.Domain.Entities.System;
using RESQ.Domain.Enum.System;
using RESQ.Infrastructure.Options;
using RESQ.Infrastructure.Services.Ai;

namespace RESQ.Tests.Infrastructure.Ai;

public class AiPromptExecutionSettingsResolverTests
{
    [Fact]
    public void Resolve_ShouldUseProviderDefaults_ForOpenRouterPrompt()
    {
        var resolver = CreateResolver(masterKey: null);
        var prompt = new PromptModel
        {
            Provider = AiProvider.OpenRouter,
            Temperature = 0.4,
            MaxTokens = 512
        };

        var settings = resolver.Resolve(prompt, new AiPromptExecutionFallback(
            GeminiModel: "gemini-fallback",
            GeminiApiUrl: "https://gemini-fallback",
            Temperature: 0.2,
            MaxTokens: 256));

        Assert.Equal(AiProvider.OpenRouter, settings.Provider);
        Assert.Equal("openrouter-default-model", settings.Model);
        Assert.Equal("https://openrouter.example/chat/completions", settings.ApiUrl);
        Assert.Equal("openrouter-provider-key", settings.ApiKey);
        Assert.Equal(0.4, settings.Temperature);
        Assert.Equal(512, settings.MaxTokens);
    }

    [Fact]
    public void Resolve_ShouldPreferPromptSecretOverride_WhenEncryptedPromptApiKeyExists()
    {
        var masterKey = "prompt-secret-master-key";
        var promptProtector = CreateProtector(masterKey);
        var resolver = CreateResolver(masterKey);
        var prompt = new PromptModel
        {
            Provider = AiProvider.Gemini,
            ApiKey = promptProtector.Protect("prompt-level-key"),
            Model = "gemini-custom",
            ApiUrl = "https://custom-gemini/{0}/{1}"
        };

        var settings = resolver.Resolve(prompt, new AiPromptExecutionFallback(
            GeminiModel: "gemini-fallback",
            GeminiApiUrl: "https://gemini-fallback/{0}/{1}",
            Temperature: 0.2,
            MaxTokens: 256));

        Assert.Equal(AiProvider.Gemini, settings.Provider);
        Assert.Equal("gemini-custom", settings.Model);
        Assert.Equal("https://custom-gemini/{0}/{1}", settings.ApiUrl);
        Assert.Equal("prompt-level-key", settings.ApiKey);
    }

    private static AiPromptExecutionSettingsResolver CreateResolver(string? masterKey)
    {
        return new AiPromptExecutionSettingsResolver(
            Options.Create(new AiProvidersOptions
            {
                Gemini = new AiProviderEndpointOptions
                {
                    ApiUrl = "https://gemini.example/{0}/{1}",
                    ApiKey = "gemini-provider-key",
                    DefaultModel = "gemini-default-model"
                },
                OpenRouter = new AiProviderEndpointOptions
                {
                    ApiUrl = "https://openrouter.example/chat/completions",
                    ApiKey = "openrouter-provider-key",
                    DefaultModel = "openrouter-default-model"
                }
            }),
            CreateProtector(masterKey));
    }

    private static PromptSecretProtector CreateProtector(string? masterKey)
    {
        return new PromptSecretProtector(Options.Create(new PromptSecretsOptions
        {
            MasterKey = masterKey
        }));
    }
}