using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Enum.Operations;
using RESQ.Application.Common.Models;

namespace RESQ.Application.Repositories.Operations;

public interface IMissionActivityRepository
{
    Task<MissionActivityModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IEnumerable<MissionActivityModel>> GetByMissionIdAsync(int missionId, CancellationToken cancellationToken = default);
    Task<IEnumerable<MissionActivityModel>> GetBySosRequestIdsAsync(IEnumerable<int> sosRequestIds, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MissionActivityModel>> GetOpenByAssemblyPointAsync(int assemblyPointId, CancellationToken cancellationToken = default);
    Task<List<AssemblyPointUnavailableRescueTeamImpactDto>> GetReassignableAssemblyPointImpactsAsync(int assemblyPointId, CancellationToken cancellationToken = default)
        => Task.FromResult<List<AssemblyPointUnavailableRescueTeamImpactDto>>([]);
    Task<HashSet<int>> ReassignAssemblyPointAsync(int sourceAssemblyPointId, IReadOnlyDictionary<int, int> activityAssignments, Guid changedBy, CancellationToken cancellationToken = default)
        => Task.FromResult<HashSet<int>>([]);
    Task<int> AddAsync(MissionActivityModel activity, CancellationToken cancellationToken = default);
    Task UpdateAsync(MissionActivityModel activity, CancellationToken cancellationToken = default);
    Task UpdateStatusAsync(int activityId, MissionActivityStatus status, Guid decisionBy, string? imageUrl = null, CancellationToken cancellationToken = default);
    Task AssignTeamAsync(int activityId, int missionTeamId, CancellationToken cancellationToken = default);
    Task ResetAssignmentsToPlannedAsync(IEnumerable<int> activityIds, Guid decisionBy, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
