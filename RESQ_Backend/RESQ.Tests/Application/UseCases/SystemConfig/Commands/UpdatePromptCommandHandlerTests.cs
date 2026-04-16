using Microsoft.Extensions.Logging.Abstractions;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
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

    [Fact]
    public async Task Handle_ShouldRejectUpdate_WhenPromptIsNotDraft()
    {
        var storedPrompt = BuildPrompt();
        storedPrompt.IsActive = true;
        storedPrompt.Version = "v1.0";

        var promptRepository = new RecordingPromptRepository(storedPrompt);
        var unitOfWork = new StubUnitOfWork();
        var handler = BuildHandler(promptRepository, unitOfWork);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(BuildCommand(storedPrompt.Id, "new-key"), CancellationToken.None));

        Assert.Equal(0, promptRepository.UpdateCalls);
        Assert.Equal(0, unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task Handle_ShouldRejectPromptTypeChange_ForDraftPrompt()
    {
        var storedPrompt = BuildPrompt();
        var promptRepository = new RecordingPromptRepository(storedPrompt);
        var unitOfWork = new StubUnitOfWork();
        var handler = BuildHandler(promptRepository, unitOfWork);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(new UpdatePromptCommand(
                storedPrompt.Id,
                Name: null,
                PromptType: PromptType.MissionDepotPlanning,
                Provider: null,
                Purpose: null,
                SystemPrompt: null,
                UserPromptTemplate: null,
                Model: null,
                Temperature: null,
                MaxTokens: null,
                Version: null,
                ApiUrl: null,
                ApiKey: null,
                IsActive: null), CancellationToken.None));

        Assert.Equal(PromptType.MissionPlanning, storedPrompt.PromptType);
        Assert.Equal(0, promptRepository.UpdateCalls);
        Assert.Equal(0, unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task Handle_ShouldRejectDuplicateDraftVersion_ForSamePromptType()
    {
        var storedPrompt = BuildPrompt();
        var promptRepository = new RecordingPromptRepository(storedPrompt)
        {
            ExistsVersionResult = true
        };
        var unitOfWork = new StubUnitOfWork();
        var handler = BuildHandler(promptRepository, unitOfWork);

        await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(new UpdatePromptCommand(
                storedPrompt.Id,
                Name: null,
                PromptType: null,
                Provider: null,
                Purpose: null,
                SystemPrompt: null,
                UserPromptTemplate: null,
                Model: null,
                Temperature: null,
                MaxTokens: null,
                Version: " v1.1-D26041612 ",
                ApiUrl: null,
                ApiKey: null,
                IsActive: null), CancellationToken.None));

        Assert.Equal(0, promptRepository.UpdateCalls);
        Assert.Equal(0, unitOfWork.SaveCalls);
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
        Version = "v1-D26041612",
        ApiUrl = "https://stored.example",
        ApiKey = "stored-key",
        IsActive = false
    };

    private sealed class RecordingPromptRepository(PromptModel? prompt) : IPromptRepository
    {
        public bool ExistsVersionResult { get; init; }
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

        public Task<bool> ExistsAsync(string name, int? excludeId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<bool> ExistsVersionAsync(PromptType promptType, string version, int? excludeId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(ExistsVersionResult);

        public Task<IReadOnlyList<PromptModel>> GetVersionsByTypeAsync(PromptType promptType, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PromptModel>>([]);

        public Task DeactivateOthersByTypeAsync(int currentPromptId, PromptType promptType, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<PagedResult<PromptModel>> GetAllPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }
}
