using RESQ.Domain.Entities.System;

namespace RESQ.Application.Repositories.System;

public interface IPromptRepository
{
    Task<PromptModel?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<PromptModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task CreateAsync(PromptModel prompt, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string name, CancellationToken cancellationToken = default);
}
