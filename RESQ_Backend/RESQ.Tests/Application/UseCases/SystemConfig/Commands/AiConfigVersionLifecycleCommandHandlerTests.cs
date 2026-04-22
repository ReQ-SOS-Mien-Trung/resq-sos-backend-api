using Microsoft.Extensions.Logging.Abstractions;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.System;
using RESQ.Application.Services.Ai;
using RESQ.Application.UseCases.SystemConfig.Commands.ActivateAiConfigVersion;
using RESQ.Application.UseCases.SystemConfig.Commands.CreateAiConfig;
using RESQ.Application.UseCases.SystemConfig.Commands.CreateAiConfigDraft;
using RESQ.Application.UseCases.SystemConfig.Commands.RollbackAiConfigVersion;
using RESQ.Domain.Entities.System;
using RESQ.Domain.Enum.System;
using RESQ.Tests.TestDoubles;

namespace RESQ.Tests.Application.UseCases.SystemConfig.Commands;

public class AiConfigVersionLifecycleCommandHandlerTests
{
    [Fact]
    public async Task CreateDraft_FromReleasedVersion_CreatesInactiveDraftWithDraftStatus()
    {
        var source = BuildConfig(id: 1, version: "v1.0", isActive: true);
        var repository = new InMemoryAiConfigRepository([source]);
        var unitOfWork = new StubUnitOfWork();
        var handler = new CreateAiConfigDraftCommandHandler(
            repository,
            unitOfWork,
            new StubAdminRealtimeHubService(),
            NullLogger<CreateAiConfigDraftCommandHandler>.Instance);

        var response = await handler.Handle(new CreateAiConfigDraftCommand(source.Id), CancellationToken.None);

        var draft = Assert.Single(repository.Items, config => config.Id != source.Id);
        Assert.False(draft.IsActive);
        Assert.StartsWith("v1.0-D", draft.Version, StringComparison.Ordinal);
        Assert.Equal(AiProviderDefaults.ResolveApiUrl(source.Provider), draft.ApiUrl);
        Assert.Equal("Draft", response.Status);
        Assert.Equal(draft.Id, response.Id);
        Assert.Equal(1, unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task Activate_NormalizesDraftVersion_AndDeactivatesCurrentActiveVersion()
    {
        var currentActive = BuildConfig(id: 1, version: "v1.0", isActive: true);
        var targetDraft = BuildConfig(id: 2, version: "v1.1-D26041612", isActive: false);
        var repository = new InMemoryAiConfigRepository([currentActive, targetDraft]);
        var unitOfWork = new StubUnitOfWork();
        var handler = new ActivateAiConfigVersionCommandHandler(
            repository,
            unitOfWork,
            new StubAdminRealtimeHubService(),
            NullLogger<ActivateAiConfigVersionCommandHandler>.Instance);

        var response = await handler.Handle(new ActivateAiConfigVersionCommand(targetDraft.Id), CancellationToken.None);

        Assert.False(currentActive.IsActive);
        Assert.Equal("v1.0", currentActive.Version);
        Assert.Equal(AiProviderDefaults.ResolveApiUrl(currentActive.Provider), currentActive.ApiUrl);
        Assert.True(targetDraft.IsActive);
        Assert.Equal("v1.1", targetDraft.Version);
        Assert.Equal(AiProviderDefaults.ResolveApiUrl(targetDraft.Provider), targetDraft.ApiUrl);
        Assert.Equal("Active", response.Status);
        Assert.Equal("v1.1", response.Version);
        Assert.Equal(1, unitOfWork.ExecuteInTransactionCalls);
        Assert.Equal(1, unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task Activate_ShouldReject_WhenNormalizedReleasedVersionAlreadyExists()
    {
        var archivedReleased = BuildConfig(id: 1, version: "v1.0", isActive: false);
        var draft = BuildConfig(id: 2, version: "v1.0-D26041612", isActive: false);
        var repository = new InMemoryAiConfigRepository([archivedReleased, draft]);
        var unitOfWork = new StubUnitOfWork();
        var handler = new ActivateAiConfigVersionCommandHandler(
            repository,
            unitOfWork,
            new StubAdminRealtimeHubService(),
            NullLogger<ActivateAiConfigVersionCommandHandler>.Instance);

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(new ActivateAiConfigVersionCommand(draft.Id), CancellationToken.None));

        Assert.Equal("AI config version 'v1.0' đã tồn tại. Hãy đổi version draft trước khi kích hoạt.", exception.Message);
        Assert.False(draft.IsActive);
        Assert.Equal("v1.0-D26041612", draft.Version);
        Assert.Equal(1, unitOfWork.ExecuteInTransactionCalls);
        Assert.Equal(0, unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task Rollback_ActivatesArchivedVersion_AndNormalizesVersion()
    {
        var archived = BuildConfig(id: 1, version: "v1.0-D26041612", isActive: false);
        var currentActive = BuildConfig(id: 2, version: "v1.1", isActive: true);
        var repository = new InMemoryAiConfigRepository([archived, currentActive]);
        var unitOfWork = new StubUnitOfWork();
        var handler = new RollbackAiConfigVersionCommandHandler(
            repository,
            unitOfWork,
            new StubAdminRealtimeHubService(),
            NullLogger<RollbackAiConfigVersionCommandHandler>.Instance);

        var response = await handler.Handle(new RollbackAiConfigVersionCommand(archived.Id), CancellationToken.None);

        Assert.True(archived.IsActive);
        Assert.Equal("v1.0", archived.Version);
        Assert.Equal(AiProviderDefaults.ResolveApiUrl(archived.Provider), archived.ApiUrl);
        Assert.False(currentActive.IsActive);
        Assert.Equal(AiProviderDefaults.ResolveApiUrl(currentActive.Provider), currentActive.ApiUrl);
        Assert.Equal("Active", response.Status);
        Assert.Equal("v1.0", response.Version);
        Assert.Equal(1, unitOfWork.ExecuteInTransactionCalls);
        Assert.Equal(1, unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task Create_ShouldReject_WhenReleasedVersionAlreadyExists()
    {
        var existing = BuildConfig(id: 1, version: "v1.0", isActive: true);
        var repository = new InMemoryAiConfigRepository([existing]);
        var unitOfWork = new StubUnitOfWork();
        var handler = new CreateAiConfigCommandHandler(
            repository,
            unitOfWork,
            new StubAdminRealtimeHubService(),
            NullLogger<CreateAiConfigCommandHandler>.Instance);

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(
                new CreateAiConfigCommand(
                    Name: "New AI config",
                    Provider: AiProvider.Gemini,
                    Model: "gemini-2.5-flash",
                    Temperature: 0.3,
                    MaxTokens: 2048,
                    ApiKey: "secret",
                    Version: " v1.0 ",
                    IsActive: false),
                CancellationToken.None));

        Assert.Equal("AI config đã tồn tại version 'v1.0'.", exception.Message);
        Assert.Equal(0, unitOfWork.SaveCalls);
    }

    private static AiConfigModel BuildConfig(int id, string version, bool isActive) => new()
    {
        Id = id,
        Name = $"AI Config #{id}",
        Provider = AiProvider.Gemini,
        Model = "gemini-2.5-flash",
        Temperature = 0.3,
        MaxTokens = 4096,
        ApiUrl = "https://stale.example.com/ai",
        ApiKey = "secret-key",
        Version = version,
        IsActive = isActive,
        CreatedAt = DateTime.UtcNow.AddHours(-1),
        UpdatedAt = DateTime.UtcNow.AddMinutes(-30)
    };

    private sealed class InMemoryAiConfigRepository(IEnumerable<AiConfigModel> configs) : IAiConfigRepository
    {
        private int _nextId = configs.Any() ? configs.Max(config => config.Id) + 1 : 1;

        public List<AiConfigModel> Items { get; } = configs.ToList();

        public Task<AiConfigModel?> GetActiveAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Items.FirstOrDefault(config => config.IsActive));

        public Task<AiConfigModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.FirstOrDefault(config => config.Id == id));

        public Task CreateAsync(AiConfigModel config, CancellationToken cancellationToken = default)
        {
            if (config.Id <= 0)
            {
                config.Id = _nextId++;
            }

            Items.Add(config);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(AiConfigModel config, CancellationToken cancellationToken = default)
        {
            var index = Items.FindIndex(existing => existing.Id == config.Id);
            if (index >= 0)
            {
                Items[index] = config;
            }

            return Task.CompletedTask;
        }

        public Task DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            Items.RemoveAll(config => config.Id == id);
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(string name, int? excludeId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.Any(config =>
                string.Equals(config.Name, name, StringComparison.OrdinalIgnoreCase)
                && (!excludeId.HasValue || config.Id != excludeId.Value)));

        public Task<bool> ExistsVersionAsync(string version, int? excludeId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.Any(config =>
                string.Equals(config.Version, version, StringComparison.OrdinalIgnoreCase)
                && (!excludeId.HasValue || config.Id != excludeId.Value)));

        public Task<IReadOnlyList<AiConfigModel>> GetVersionsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AiConfigModel>>(Items
                .OrderByDescending(config => config.IsActive)
                .ThenByDescending(config => config.UpdatedAt ?? config.CreatedAt)
                .ThenByDescending(config => config.Id)
                .ToList());

        public Task DeactivateOthersAsync(int currentConfigId, CancellationToken cancellationToken = default)
        {
            foreach (var config in Items.Where(config => config.IsActive && config.Id != currentConfigId))
            {
                config.IsActive = false;
            }

            return Task.CompletedTask;
        }

        public Task<PagedResult<AiConfigModel>> GetAllPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }
}
