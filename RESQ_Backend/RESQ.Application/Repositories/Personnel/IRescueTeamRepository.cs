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

    /// <summary>Kiểm tra user có đang là đội trưởng của bất kỳ đội cứu hộ đang hoạt động nào không.</summary>
    Task<bool> IsLeaderInActiveTeamAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy UserId của đội trưởng trong đội hiện tại mà <paramref name="memberUserId"/> đang tham gia (Accepted, chưa Disbanded).
    /// Trả về null nếu không tìm thấy hoặc đội chưa có trưởng.
    /// </summary>
    Task<Guid?> GetTeamLeaderUserIdByMemberAsync(Guid memberUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-remove rescuer khỏi đội đang tham gia (set Status = Removed trực tiếp trong DB).
    /// Không phụ thuộc vào trạng thái đội. Trả về false nếu rescuer không đang trong bất kỳ đội nào.
    /// </summary>
    Task<bool> SoftRemoveMemberFromActiveTeamAsync(Guid memberUserId, CancellationToken cancellationToken = default);
    Task<bool> HasRequiredAbilityCategoryAsync(Guid userId, string categoryCode, CancellationToken cancellationToken = default);
    Task<string?> GetTopAbilityCategoryAsync(Guid userId, CancellationToken cancellationToken = default);
    Task CreateAsync(RescueTeamModel team, CancellationToken cancellationToken = default);
    Task UpdateAsync(RescueTeamModel team, CancellationToken cancellationToken = default);

    /// <summary>Dếm số đội (chưa Disbanded) hiện đang gán vào điểm tập kết, trừ các đội trong <paramref name="excludeTeamIds"/>.</summary>
    Task<int> CountActiveTeamsByAssemblyPointAsync(int assemblyPointId, IEnumerable<int> excludeTeamIds, CancellationToken cancellationToken = default);

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
