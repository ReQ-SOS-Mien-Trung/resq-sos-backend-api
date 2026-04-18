using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Application.Repositories.Operations;

public interface IMissionActivityRepository
{
    Task<MissionActivityModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IEnumerable<MissionActivityModel>> GetByMissionIdAsync(int missionId, CancellationToken cancellationToken = default);
    Task<IEnumerable<MissionActivityModel>> GetBySosRequestIdsAsync(IEnumerable<int> sosRequestIds, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MissionActivityModel>> GetOpenByAssemblyPointAsync(int assemblyPointId, CancellationToken cancellationToken = default);
    Task<int> AddAsync(MissionActivityModel activity, CancellationToken cancellationToken = default);
    Task UpdateAsync(MissionActivityModel activity, CancellationToken cancellationToken = default);
    Task UpdateStatusAsync(int activityId, MissionActivityStatus status, Guid decisionBy, string? imageUrl = null, CancellationToken cancellationToken = default);
    Task AssignTeamAsync(int activityId, int missionTeamId, CancellationToken cancellationToken = default);
    Task ResetAssignmentsToPlannedAsync(IEnumerable<int> activityIds, Guid decisionBy, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
