using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Application.Repositories.Operations;

public interface ITeamIncidentRepository
{
    Task<IEnumerable<TeamIncidentModel>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<RESQ.Application.Common.Models.PagedResult<TeamIncidentModel>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<TeamIncidentModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IEnumerable<TeamIncidentModel>> GetByMissionIdAsync(int missionId, CancellationToken cancellationToken = default);
    Task<RESQ.Application.Common.Models.PagedResult<TeamIncidentModel>> GetPagedByMissionIdAsync(int missionId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<IEnumerable<TeamIncidentModel>> GetByMissionTeamIdAsync(int missionTeamId, CancellationToken cancellationToken = default);
    Task<int> CreateAsync(TeamIncidentModel model, CancellationToken cancellationToken = default);
    Task UpdateStatusAsync(int id, TeamIncidentStatus status, CancellationToken cancellationToken = default);
    Task UpdateSupportSosRequestIdAsync(int id, int? supportSosRequestId, CancellationToken cancellationToken = default);
}
