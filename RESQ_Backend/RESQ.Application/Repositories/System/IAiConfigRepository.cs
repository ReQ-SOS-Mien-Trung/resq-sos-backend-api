using RESQ.Application.Common.Models;
using RESQ.Domain.Entities.System;

namespace RESQ.Application.Repositories.System;

public interface IAiConfigRepository
{
    Task<AiConfigModel?> GetActiveAsync(CancellationToken cancellationToken = default);
    Task<AiConfigModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task CreateAsync(AiConfigModel config, CancellationToken cancellationToken = default);
    Task UpdateAsync(AiConfigModel config, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string name, int? excludeId = null, CancellationToken cancellationToken = default);
    Task<bool> ExistsVersionAsync(string version, int? excludeId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AiConfigModel>> GetVersionsAsync(CancellationToken cancellationToken = default);
    Task DeactivateOthersAsync(int currentConfigId, CancellationToken cancellationToken = default);
    Task<PagedResult<AiConfigModel>> GetAllPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
}
