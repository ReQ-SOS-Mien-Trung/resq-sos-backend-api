using Microsoft.Extensions.Logging.Abstractions;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.System;
using RESQ.Application.Services.Ai;
using RESQ.Application.UseCases.SystemConfig.Commands.UpdateAiConfig;
using RESQ.Domain.Entities.System;
using RESQ.Domain.Enum.System;
using RESQ.Tests.TestDoubles;

namespace RESQ.Tests.Application.UseCases.SystemConfig.Commands;

public class UpdateAiConfigCommandHandlerTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abcd...5678")]
    [InlineData("*******")]
    public async Task Handle_ShouldKeepStoredApiKey_WhenSubmittedApiKeyIsMissingMaskedOrWhitespace(string? submittedApiKey)
    {
        var storedConfig = BuildConfig();
        var repository = new RecordingAiConfigRepository(storedConfig);
        var unitOfWork = new StubUnitOfWork();
        var handler = new UpdateAiConfigCommandHandler(
            repository,
            unitOfWork,
            new StubAdminRealtimeHubService(),
            NullLogger<UpdateAiConfigCommandHandler>.Instance);

        await handler.Handle(new UpdateAiConfigCommand(
            storedConfig.Id,
            Name: null,
            Provider: null,
            Model: null,
            Temperature: null,
            MaxTokens: null,
            ApiKey: submittedApiKey,
            Version: null,
            IsActive: null), CancellationToken.None);

        Assert.Equal("stored-key", storedConfig.ApiKey);
        Assert.Same(storedConfig, repository.UpdatedConfig);
        Assert.Equal(1, repository.UpdateCalls);
        Assert.Equal(1, unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task Handle_ShouldUpdateStoredApiKey_WhenSubmittedApiKeyIsRawValue()
    {
        var storedConfig = BuildConfig();
        var repository = new RecordingAiConfigRepository(storedConfig);
        var unitOfWork = new StubUnitOfWork();
        var handler = new UpdateAiConfigCommandHandler(
            repository,
            unitOfWork,
            new StubAdminRealtimeHubService(),
            NullLogger<UpdateAiConfigCommandHandler>.Instance);

        await handler.Handle(new UpdateAiConfigCommand(
            storedConfig.Id,
            Name: null,
            Provider: null,
            Model: null,
            Temperature: null,
            MaxTokens: null,
            ApiKey: "new-raw-key",
            Version: null,
            IsActive: null), CancellationToken.None);

        Assert.Equal("new-raw-key", storedConfig.ApiKey);
        Assert.Same(storedConfig, repository.UpdatedConfig);
        Assert.Equal(1, repository.UpdateCalls);
        Assert.Equal(1, unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task Handle_ShouldNormalizeApiUrl_WhenProviderChanges()
    {
        var storedConfig = BuildConfig();
        var repository = new RecordingAiConfigRepository(storedConfig);
        var unitOfWork = new StubUnitOfWork();
        var handler = new UpdateAiConfigCommandHandler(
            repository,
            unitOfWork,
            new StubAdminRealtimeHubService(),
            NullLogger<UpdateAiConfigCommandHandler>.Instance);

        await handler.Handle(new UpdateAiConfigCommand(
            storedConfig.Id,
            Name: null,
            Provider: AiProvider.OpenRouter,
            Model: null,
            Temperature: null,
            MaxTokens: null,
            ApiKey: null,
            Version: null,
            IsActive: null), CancellationToken.None);

        Assert.Equal(AiProvider.OpenRouter, storedConfig.Provider);
        Assert.Equal(AiProviderDefaults.OpenRouterApiUrl, storedConfig.ApiUrl);
        Assert.Equal(1, repository.UpdateCalls);
        Assert.Equal(1, unitOfWork.SaveCalls);
    }

    private static AiConfigModel BuildConfig() => new()
    {
        Id = 1,
        Name = "Stored AI config",
        Provider = AiProvider.Gemini,
        Model = "stored-model",
        Temperature = 0.2,
        MaxTokens = 4096,
        ApiUrl = "https://stored.example",
        ApiKey = "stored-key",
        Version = "v1-D26041612",
        IsActive = false
    };

    private sealed class RecordingAiConfigRepository(AiConfigModel? config) : IAiConfigRepository
    {
        public int UpdateCalls { get; private set; }
        public AiConfigModel? UpdatedConfig { get; private set; }

        public Task<AiConfigModel?> GetActiveAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(config?.IsActive == true ? config : null);

        public Task<AiConfigModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(config?.Id == id ? config : null);

        public Task CreateAsync(AiConfigModel config, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpdateAsync(AiConfigModel config, CancellationToken cancellationToken = default)
        {
            UpdateCalls++;
            UpdatedConfig = config;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(int id, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<bool> ExistsAsync(string name, int? excludeId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<bool> ExistsVersionAsync(string version, int? excludeId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<IReadOnlyList<AiConfigModel>> GetVersionsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AiConfigModel>>([]);

        public Task DeactivateOthersAsync(int currentConfigId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<PagedResult<AiConfigModel>> GetAllPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }
}
