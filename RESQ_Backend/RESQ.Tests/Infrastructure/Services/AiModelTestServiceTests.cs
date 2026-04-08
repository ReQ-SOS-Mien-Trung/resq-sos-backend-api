using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RESQ.Application.Services;
using RESQ.Application.Services.Ai;
using RESQ.Domain.Entities.System;
using RESQ.Domain.Enum.System;
using RESQ.Infrastructure.Options;
using RESQ.Infrastructure.Services;
using RESQ.Infrastructure.Services.Ai;

namespace RESQ.Tests.Infrastructure.Services;

public class AiModelTestServiceTests
{
    [Fact]
    public async Task TestModelAsync_ShouldRouteToPromptProvider_FromPromptConfiguration()
    {
        var resolver = new AiPromptExecutionSettingsResolver(
            Options.Create(new AiProvidersOptions
            {
                OpenRouter = new AiProviderEndpointOptions
                {
                    ApiUrl = "https://openrouter.example/chat/completions",
                    ApiKey = "openrouter-provider-key",
                    DefaultModel = "openai/gpt-4o-mini"
                }
            }),
            new PromptSecretProtector(Options.Create(new PromptSecretsOptions
            {
                MasterKey = null
            })));
        var factory = new RecordingAiProviderClientFactory(new RecordingAiProviderClient());
        IAiModelTestService service = new AiModelTestService(factory, resolver, NullLogger<AiModelTestService>.Instance);

        var result = await service.TestModelAsync(new PromptModel
        {
            Provider = AiProvider.OpenRouter
        });

        Assert.True(result.IsSuccess);
        Assert.Equal("openai/gpt-4o-mini", result.Model);
        Assert.Equal(AiProvider.OpenRouter, factory.RequestedProvider);
    }

    private sealed class RecordingAiProviderClientFactory(RecordingAiProviderClient client) : IAiProviderClientFactory
    {
        private readonly RecordingAiProviderClient _client = client;

        public AiProvider RequestedProvider { get; private set; }

        public IAiProviderClient GetClient(AiProvider provider)
        {
            RequestedProvider = provider;
            _client.ProviderOverride = provider;
            return _client;
        }
    }

    private sealed class RecordingAiProviderClient : IAiProviderClient
    {
        public AiProvider ProviderOverride { get; set; } = AiProvider.Gemini;

        public AiProvider Provider => ProviderOverride;

        public Task<AiCompletionResponse> CompleteAsync(AiCompletionRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new AiCompletionResponse
            {
                Text = "{\"status\":\"ok\"}",
                HttpStatusCode = 200
            });
        }
    }
}