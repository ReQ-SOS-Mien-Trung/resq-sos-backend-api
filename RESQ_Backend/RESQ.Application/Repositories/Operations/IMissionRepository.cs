using RESQ.Domain.Entities.Operations;

namespace RESQ.Application.Repositories.Operations;

public interface IMissionRepository
{
    Task<MissionModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IEnumerable<MissionModel>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<MissionModel>> GetByClusterIdAsync(int clusterId, CancellationToken cancellationToken = default);
    Task<int> CreateAsync(MissionModel mission, Guid coordinatorId, CancellationToken cancellationToken = default);
    Task UpdateAsync(MissionModel mission, CancellationToken cancellationToken = default);
    Task UpdateStatusAsync(int missionId, string status, bool isCompleted, CancellationToken cancellationToken = default);
}
