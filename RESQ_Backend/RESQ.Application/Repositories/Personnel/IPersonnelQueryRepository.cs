using RESQ.Application.Common.Models;
using RESQ.Domain.Entities.Personnel;

namespace RESQ.Application.Repositories.Personnel;

public interface IPersonnelQueryRepository
{
    Task<PagedResult<FreeRescuerModel>> GetFreeRescuersAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<PagedResult<RescueTeamModel>> GetAllRescueTeamsAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<RescueTeamModel?> GetRescueTeamDetailAsync(int teamId, CancellationToken cancellationToken = default);
}
