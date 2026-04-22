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
    [Fact]
    public async Task Handle_ShouldUpdateDraftFields_WhenRequestIsValid()
    {
        var storedPrompt = BuildPrompt();
        var promptRepository = new RecordingPromptRepository(storedPrompt);
        var unitOfWork = new StubUnitOfWork();
        var handler = BuildHandler(promptRepository, unitOfWork);

        await handler.Handle(new UpdatePromptCommand(
            storedPrompt.Id,
            Name: "Updated prompt",
            PromptType: null,
            Purpose: "Updated purpose",
            SystemPrompt: "updated system",
            UserPromptTemplate: "updated user",
            Version: "v1.1-D26041612",
            IsActive: null), CancellationToken.None);

        Assert.Equal("Updated prompt", storedPrompt.Name);
        Assert.Equal("Updated purpose", storedPrompt.Purpose);
        Assert.Equal("updated system", storedPrompt.SystemPrompt);
        Assert.Equal("updated user", storedPrompt.UserPromptTemplate);
        Assert.Equal("v1.1-D26041612", storedPrompt.Version);
        Assert.False(storedPrompt.IsActive);
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
            handler.Handle(new UpdatePromptCommand(
                storedPrompt.Id,
                Name: "Updated",
                PromptType: null,
                Purpose: null,
                SystemPrompt: null,
                UserPromptTemplate: null,
                Version: null,
                IsActive: null), CancellationToken.None));

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
                Purpose: null,
                SystemPrompt: null,
                UserPromptTemplate: null,
                Version: null,
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
                Purpose: null,
                SystemPrompt: null,
                UserPromptTemplate: null,
                Version: " v1.1-D26041612 ",
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
            new StubAdminRealtimeHubService(),
            NullLogger<UpdatePromptCommandHandler>.Instance);
    }

    private static PromptModel BuildPrompt() => new()
    {
        Id = 1,
        Name = "Stored prompt",
        PromptType = PromptType.MissionPlanning,
        Purpose = "Stored purpose",
        SystemPrompt = "stored system",
        UserPromptTemplate = "stored user",
        Version = "v1-D26041612",
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
