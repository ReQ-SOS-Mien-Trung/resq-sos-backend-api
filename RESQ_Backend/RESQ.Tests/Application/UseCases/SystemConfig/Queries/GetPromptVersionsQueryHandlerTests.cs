using Microsoft.Extensions.Logging.Abstractions;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.System;
using RESQ.Application.UseCases.SystemConfig.Queries.GetPromptVersions;
using RESQ.Domain.Entities.System;
using RESQ.Domain.Enum.System;

namespace RESQ.Tests.Application.UseCases.SystemConfig.Queries;

public class GetPromptVersionsQueryHandlerTests
{
    [Fact]
    public async Task Handle_OrdersVersions_AsActiveThenDraftThenArchived()
    {
        var active = BuildPrompt(id: 1, version: "v2.0", isActive: true, updatedAt: DateTime.UtcNow);
        var draft = BuildPrompt(id: 2, version: "v3.0-D26041612", isActive: false, updatedAt: DateTime.UtcNow.AddMinutes(-1));
        var archived = BuildPrompt(id: 3, version: "v1.0", isActive: false, updatedAt: DateTime.UtcNow.AddMinutes(-2));
        var repository = new PromptRepositoryStub([active, draft, archived]);
        var handler = new GetPromptVersionsQueryHandler(repository, NullLogger<GetPromptVersionsQueryHandler>.Instance);

        var result = await handler.Handle(new GetPromptVersionsQuery(active.Id), CancellationToken.None);

        Assert.Equal([active.Id, draft.Id, archived.Id], result.Items.Select(item => item.Id).ToArray());
        Assert.Equal(["Active", "Draft", "Archived"], result.Items.Select(item => item.Status).ToArray());
    }

    private static PromptModel BuildPrompt(int id, string version, bool isActive, DateTime updatedAt) => new()
    {
        Id = id,
        Name = $"Prompt #{id}",
        PromptType = PromptType.MissionPlanning,
        Purpose = "purpose",
        SystemPrompt = "system",
        UserPromptTemplate = "user",
        Version = version,
        IsActive = isActive,
        CreatedAt = updatedAt.AddMinutes(-5),
        UpdatedAt = updatedAt
    };

    private sealed class PromptRepositoryStub(IEnumerable<PromptModel> prompts) : IPromptRepository
    {
        private readonly List<PromptModel> _prompts = prompts.ToList();

        public Task<PromptModel?> GetActiveByTypeAsync(PromptType promptType, CancellationToken cancellationToken = default)
            => Task.FromResult(_prompts.FirstOrDefault(prompt => prompt.PromptType == promptType && prompt.IsActive));

        public Task<PromptModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(_prompts.FirstOrDefault(prompt => prompt.Id == id));

        public Task CreateAsync(PromptModel prompt, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpdateAsync(PromptModel prompt, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DeleteAsync(int id, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<bool> ExistsAsync(string name, int? excludeId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<bool> ExistsVersionAsync(PromptType promptType, string version, int? excludeId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<IReadOnlyList<PromptModel>> GetVersionsByTypeAsync(PromptType promptType, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PromptModel>>(_prompts.Where(prompt => prompt.PromptType == promptType).ToList());

        public Task DeactivateOthersByTypeAsync(int currentPromptId, PromptType promptType, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<PagedResult<PromptModel>> GetAllPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }
}
