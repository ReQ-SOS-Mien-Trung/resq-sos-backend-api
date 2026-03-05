using RESQ.Application.Common.Models;
using RESQ.Domain.Entities.System;
using RESQ.Domain.Enum.System;

namespace RESQ.Application.Repositories.System;

public interface IPromptRepository
{
    /// <summary>
    /// Lấy prompt đang IsActive=true theo PromptType. AI dùng phương thức này để lấy cấu hình.
    /// </summary>
    Task<PromptModel?> GetActiveByTypeAsync(PromptType promptType, CancellationToken cancellationToken = default);
    Task<PromptModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task CreateAsync(PromptModel prompt, CancellationToken cancellationToken = default);
    Task UpdateAsync(PromptModel prompt, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string name, CancellationToken cancellationToken = default);
    Task DeactivateOthersByTypeAsync(int currentPromptId, PromptType promptType, CancellationToken cancellationToken = default);
    Task<PagedResult<PromptModel>> GetAllPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
}
