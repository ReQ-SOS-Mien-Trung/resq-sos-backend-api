using Microsoft.Extensions.Logging.Abstractions;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.System;
using RESQ.Application.UseCases.SystemConfig.Commands.UpdatePrompt;
using RESQ.Domain.Entities.System;
using RESQ.Domain.Enum.System;
using RESQ.Tests.TestDoubles;

namespace RESQ.Tests.Application.UseCases.SystemConfig.Commands;

public class UpdatePromptCommandHandlerTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abcd...5678")]
    [InlineData("*******")]
    public async Task Handle_ShouldKeepStoredApiKey_WhenSubmittedApiKeyIsMissingMaskedOrWhitespace(string? submittedApiKey)
    {
        var storedPrompt = BuildPrompt();
        var promptRepository = new RecordingPromptRepository(storedPrompt);
        var unitOfWork = new StubUnitOfWork();
        var handler = BuildHandler(promptRepository, unitOfWork);

        await handler.Handle(BuildCommand(storedPrompt.Id, submittedApiKey), CancellationToken.None);

        Assert.Equal("stored-key", storedPrompt.ApiKey);
        Assert.Same(storedPrompt, promptRepository.UpdatedPrompt);
        Assert.Equal(1, promptRepository.UpdateCalls);
        Assert.Equal(1, unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task Handle_ShouldUpdateStoredApiKey_WhenSubmittedApiKeyIsRawValue()
    {
        var storedPrompt = BuildPrompt();
        var promptRepository = new RecordingPromptRepository(storedPrompt);
        var unitOfWork = new StubUnitOfWork();
        var handler = BuildHandler(promptRepository, unitOfWork);

        await handler.Handle(BuildCommand(storedPrompt.Id, "new-raw-key"), CancellationToken.None);

        Assert.Equal("new-raw-key", storedPrompt.ApiKey);
        Assert.Same(storedPrompt, promptRepository.UpdatedPrompt);
        Assert.Equal(1, promptRepository.UpdateCalls);
        Assert.Equal(1, unitOfWork.SaveCalls);
    }

    private static UpdatePromptCommandHandler BuildHandler(
        RecordingPromptRepository promptRepository,
        StubUnitOfWork unitOfWork)
    {
        return new UpdatePromptCommandHandler(
            promptRepository,
            unitOfWork,
            NullLogger<UpdatePromptCommandHandler>.Instance);
    }

    private static UpdatePromptCommand BuildCommand(int promptId, string? apiKey) => new(
        promptId,
        Name: null,
        PromptType: null,
        Provider: null,
        Purpose: null,
        SystemPrompt: null,
        UserPromptTemplate: null,
        Model: null,
        Temperature: null,
        MaxTokens: null,
        Version: null,
        ApiUrl: null,
        ApiKey: apiKey,
        IsActive: null);

    private static PromptModel BuildPrompt() => new()
    {
        Id = 1,
        Name = "Stored prompt",
        PromptType = PromptType.MissionPlanning,
        Provider = AiProvider.Gemini,
        Purpose = "Stored purpose",
        SystemPrompt = "stored system",
        UserPromptTemplate = "stored user",
        Model = "stored-model",
        Temperature = 0.2,
        MaxTokens = 4096,
        Version = "stored-version",
        ApiUrl = "https://stored.example",
        ApiKey = "stored-key",
        IsActive = true
    };

    private sealed class RecordingPromptRepository(PromptModel? prompt) : IPromptRepository
    {
        public int UpdateCalls { get; private set; }
        public PromptModel? UpdatedPrompt { get; private set; }

        public Task<PromptModel?> GetActiveByTypeAsync(PromptType promptType, CancellationToken cancellationToken = default)
            => Task.FromResult(prompt?.PromptType == promptType && prompt.IsActive ? prompt : null);

        public Task<PromptModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(prompt?.Id == id ? prompt : null);

        public Task CreateAsync(PromptModel prompt, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpdateAsync(PromptModel prompt, CancellationToken cancellationToken = default)
        {
            UpdateCalls++;
            UpdatedPrompt = prompt;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(int id, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<bool> ExistsAsync(string name, CancellationToken cancellationToken = default) => Task.FromResult(false);

        public Task DeactivateOthersByTypeAsync(int currentPromptId, PromptType promptType, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<PagedResult<PromptModel>> GetAllPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }
}
