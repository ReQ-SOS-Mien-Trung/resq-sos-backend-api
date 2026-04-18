using Microsoft.Extensions.Logging.Abstractions;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.System;
using RESQ.Application.UseCases.SystemConfig.Commands.ActivatePromptVersion;
using RESQ.Application.UseCases.SystemConfig.Commands.CreatePrompt;
using RESQ.Application.UseCases.SystemConfig.Commands.CreatePromptDraft;
using RESQ.Application.UseCases.SystemConfig.Commands.RollbackPromptVersion;
using RESQ.Domain.Entities.System;
using RESQ.Domain.Enum.System;
using RESQ.Tests.TestDoubles;

namespace RESQ.Tests.Application.UseCases.SystemConfig.Commands;

public class PromptVersionLifecycleCommandHandlerTests
{
    [Fact]
    public async Task CreateDraft_FromReleasedVersion_CreatesInactiveDraftWithDraftStatus()
    {
        var source = BuildPrompt(id: 1, version: "v1.0", isActive: true);
        var promptRepository = new InMemoryPromptRepository([source]);
        var unitOfWork = new StubUnitOfWork();
        var handler = new CreatePromptDraftCommandHandler(
            promptRepository,
            unitOfWork,
            NullLogger<CreatePromptDraftCommandHandler>.Instance);

        var response = await handler.Handle(new CreatePromptDraftCommand(source.Id), CancellationToken.None);

        var draft = Assert.Single(promptRepository.Items, prompt => prompt.Id != source.Id);
        Assert.False(draft.IsActive);
        Assert.StartsWith("v1.0-D", draft.Version, StringComparison.Ordinal);
        Assert.Equal("Draft", response.Status);
        Assert.Equal(draft.Id, response.Id);
        Assert.Equal(1, unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task Activate_NormalizesDraftVersion_AndSelfHealsLegacyActiveDraftVersion()
    {
        var legacyActive = BuildPrompt(id: 1, version: "v1.0-D26041611", isActive: true);
        var targetDraft = BuildPrompt(id: 2, version: "v1.1-D26041612", isActive: false);
        var promptRepository = new InMemoryPromptRepository([legacyActive, targetDraft]);
        var unitOfWork = new StubUnitOfWork();
        var handler = new ActivatePromptVersionCommandHandler(
            promptRepository,
            unitOfWork,
            NullLogger<ActivatePromptVersionCommandHandler>.Instance);

        var response = await handler.Handle(new ActivatePromptVersionCommand(targetDraft.Id), CancellationToken.None);

        Assert.False(legacyActive.IsActive);
        Assert.Equal("v1.0", legacyActive.Version);
        Assert.True(targetDraft.IsActive);
        Assert.Equal("v1.1", targetDraft.Version);
        Assert.Equal("Active", response.Status);
        Assert.Equal("v1.1", response.Version);
        Assert.Equal(1, unitOfWork.ExecuteInTransactionCalls);
        Assert.Equal(1, unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task Activate_ShouldReject_WhenNormalizedReleasedVersionAlreadyExists()
    {
        var archivedReleased = BuildPrompt(id: 1, version: "v1.0", isActive: false);
        var draft = BuildPrompt(id: 2, version: "v1.0-D26041612", isActive: false);
        var promptRepository = new InMemoryPromptRepository([archivedReleased, draft]);
        var unitOfWork = new StubUnitOfWork();
        var handler = new ActivatePromptVersionCommandHandler(
            promptRepository,
            unitOfWork,
            NullLogger<ActivatePromptVersionCommandHandler>.Instance);

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(new ActivatePromptVersionCommand(draft.Id), CancellationToken.None));

        Assert.Equal(
            "Version phát hành 'v1.0' của prompt type 'MissionPlanning' đã tồn tại. Hãy đổi version draft trước khi kích hoạt.",
            exception.Message);
        Assert.False(draft.IsActive);
        Assert.Equal("v1.0-D26041612", draft.Version);
        Assert.Equal(1, unitOfWork.ExecuteInTransactionCalls);
        Assert.Equal(0, unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task Rollback_AllowsLegacyInactiveDraftVersion_AndNormalizesItWhenReactivated()
    {
        var legacyPreviousVersion = BuildPrompt(id: 1, version: "v1.0-D26041612", isActive: false);
        var currentActive = BuildPrompt(id: 2, version: "v1.1", isActive: true);
        var promptRepository = new InMemoryPromptRepository([legacyPreviousVersion, currentActive]);
        var unitOfWork = new StubUnitOfWork();
        var handler = new RollbackPromptVersionCommandHandler(
            promptRepository,
            unitOfWork,
            NullLogger<RollbackPromptVersionCommandHandler>.Instance);

        var response = await handler.Handle(new RollbackPromptVersionCommand(legacyPreviousVersion.Id), CancellationToken.None);

        Assert.True(legacyPreviousVersion.IsActive);
        Assert.Equal("v1.0", legacyPreviousVersion.Version);
        Assert.False(currentActive.IsActive);
        Assert.Equal("v1.1", currentActive.Version);
        Assert.Equal("Active", response.Status);
        Assert.Equal("v1.0", response.Version);
        Assert.Equal(1, unitOfWork.ExecuteInTransactionCalls);
        Assert.Equal(1, unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task Create_ShouldReject_WhenReleasedVersionAlreadyExistsForSamePromptType()
    {
        var existing = BuildPrompt(id: 1, version: "v1.0", isActive: true);
        var promptRepository = new InMemoryPromptRepository([existing]);
        var unitOfWork = new StubUnitOfWork();
        var handler = new CreatePromptCommandHandler(
            promptRepository,
            unitOfWork,
            NullLogger<CreatePromptCommandHandler>.Instance);

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(
                new CreatePromptCommand(
                    Name: "New prompt",
                    PromptType: PromptType.MissionPlanning,
                    Purpose: "purpose",
                    SystemPrompt: "system",
                    UserPromptTemplate: "user",
                    Version: " v1.0 ",
                    IsActive: false),
                CancellationToken.None));

        Assert.Equal("Prompt type 'MissionPlanning' đã tồn tại version 'v1.0'.", exception.Message);
        Assert.Equal(0, unitOfWork.SaveCalls);
    }

    private static PromptModel BuildPrompt(int id, string version, bool isActive) => new()
    {
        Id = id,
        Name = $"Prompt #{id}",
        PromptType = PromptType.MissionPlanning,
        Purpose = "Prompt purpose",
        SystemPrompt = "system",
        UserPromptTemplate = "user",
        Version = version,
        IsActive = isActive,
        CreatedAt = DateTime.UtcNow.AddHours(-1),
        UpdatedAt = DateTime.UtcNow.AddMinutes(-30)
    };

    private sealed class InMemoryPromptRepository(IEnumerable<PromptModel> prompts) : IPromptRepository
    {
        private int _nextId = prompts.Any() ? prompts.Max(prompt => prompt.Id) + 1 : 1;

        public List<PromptModel> Items { get; } = prompts.ToList();

        public Task<PromptModel?> GetActiveByTypeAsync(PromptType promptType, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.FirstOrDefault(prompt => prompt.PromptType == promptType && prompt.IsActive));

        public Task<PromptModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.FirstOrDefault(prompt => prompt.Id == id));

        public Task CreateAsync(PromptModel prompt, CancellationToken cancellationToken = default)
        {
            if (prompt.Id <= 0)
            {
                prompt.Id = _nextId++;
            }

            Items.Add(prompt);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(PromptModel prompt, CancellationToken cancellationToken = default)
        {
            var index = Items.FindIndex(existing => existing.Id == prompt.Id);
            if (index >= 0)
            {
                Items[index] = prompt;
            }

            return Task.CompletedTask;
        }

        public Task DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            Items.RemoveAll(prompt => prompt.Id == id);
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(string name, int? excludeId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.Any(prompt =>
                string.Equals(prompt.Name, name, StringComparison.OrdinalIgnoreCase)
                && (!excludeId.HasValue || prompt.Id != excludeId.Value)));

        public Task<bool> ExistsVersionAsync(PromptType promptType, string version, int? excludeId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.Any(prompt =>
                prompt.PromptType == promptType
                && string.Equals(prompt.Version, version, StringComparison.OrdinalIgnoreCase)
                && (!excludeId.HasValue || prompt.Id != excludeId.Value)));

        public Task<IReadOnlyList<PromptModel>> GetVersionsByTypeAsync(PromptType promptType, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PromptModel>>(Items
                .Where(prompt => prompt.PromptType == promptType)
                .OrderByDescending(prompt => prompt.IsActive)
                .ThenByDescending(prompt => prompt.UpdatedAt ?? prompt.CreatedAt)
                .ThenByDescending(prompt => prompt.Id)
                .ToList());

        public Task DeactivateOthersByTypeAsync(int currentPromptId, PromptType promptType, CancellationToken cancellationToken = default)
        {
            foreach (var prompt in Items.Where(prompt => prompt.PromptType == promptType && prompt.IsActive && prompt.Id != currentPromptId))
            {
                prompt.IsActive = false;
            }

            return Task.CompletedTask;
        }

        public Task<PagedResult<PromptModel>> GetAllPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }
}
