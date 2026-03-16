using RESQ.Application.Common.Models;
using RESQ.Domain.Entities.Personnel;
using RESQ.Domain.Enum.Identity;

namespace RESQ.Application.Repositories.Personnel;

public interface IPersonnelQueryRepository
{
    Task<PagedResult<FreeRescuerModel>> GetFreeRescuersAsync(
        int pageNumber, int pageSize,
        string? firstName = null, string? lastName = null,
        string? phone = null, string? email = null,
        RescuerType? rescuerType = null,
        CancellationToken cancellationToken = default);
    Task<PagedResult<RescueTeamModel>> GetAllRescueTeamsAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<RescueTeamModel?> GetRescueTeamDetailAsync(int teamId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy đội cứu hộ "active" mà user hiện tại đang thuộc về (Accepted + team chưa Disbanded).
    /// </summary>
    Task<RescueTeamModel?> GetActiveRescueTeamByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
}
