using RESQ.Application.Common.Models;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Personnel;

namespace RESQ.Application.Repositories.Personnel;

public interface IRescueTeamRepository
{
    Task<RescueTeamModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<RescueTeamModel?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<PagedResult<RescueTeamModel>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<bool> IsUserInActiveTeamAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> HasRequiredAbilityCategoryAsync(Guid userId, string categoryCode, CancellationToken cancellationToken = default);
    Task<string?> GetTopAbilityCategoryAsync(Guid userId, CancellationToken cancellationToken = default);
    Task CreateAsync(RescueTeamModel team, CancellationToken cancellationToken = default);
    Task UpdateAsync(RescueTeamModel team, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tìm kiếm đội cứu hộ theo khả năng/trạng thái để agent AI dùng trong quá trình lập kế hoạch.
    /// </summary>
    Task<(List<AgentTeamInfo> Teams, int TotalCount)> GetTeamsForAgentAsync(
        string? abilityKeyword,
        bool? available,
        int page,
        int pageSize,
        CancellationToken ct = default);
}
