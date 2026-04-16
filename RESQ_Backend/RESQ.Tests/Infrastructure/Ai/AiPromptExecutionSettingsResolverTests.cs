using RESQ.Domain.Entities.System;
using RESQ.Domain.Enum.System;
using RESQ.Infrastructure.Services.Ai;

namespace RESQ.Tests.Infrastructure.Ai;

public class AiPromptExecutionSettingsResolverTests
{
    [Fact]
    public void Resolve_ShouldMapExecutionSettings_FromAiConfig()
    {
        var resolver = new AiPromptExecutionSettingsResolver();
        var aiConfig = new AiConfigModel
        {
            Provider = AiProvider.OpenRouter,
            Model = "openai/gpt-4o-mini",
            ApiUrl = "https://openrouter.example/chat/completions",
            ApiKey = "provider-key",
            Temperature = 0.4,
            MaxTokens = 512
        };

        var settings = resolver.Resolve(aiConfig);

        Assert.Equal(AiProvider.OpenRouter, settings.Provider);
        Assert.Equal("openai/gpt-4o-mini", settings.Model);
        Assert.Equal("https://openrouter.example/chat/completions", settings.ApiUrl);
        Assert.Equal("provider-key", settings.ApiKey);
        Assert.Equal(0.4, settings.Temperature);
        Assert.Equal(512, settings.MaxTokens);
    }

    [Fact]
    public void Resolve_ShouldKeepPlainApiKey_FromRepositoryDomainModel()
    {
        var resolver = new AiPromptExecutionSettingsResolver();
        var aiConfig = new AiConfigModel
        {
            Provider = AiProvider.Gemini,
            Model = "gemini-2.5-flash",
            ApiUrl = "https://gemini.example/{0}/{1}",
            ApiKey = "plain-ai-key",
            Temperature = 0.2,
            MaxTokens = 2048
        };

        var settings = resolver.Resolve(aiConfig);

        Assert.Equal("plain-ai-key", settings.ApiKey);
    }
}
