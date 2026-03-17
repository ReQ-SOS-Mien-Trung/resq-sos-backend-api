using RESQ.Domain.Entities.Operations;

namespace RESQ.Application.Repositories.Operations;

public interface IMissionTeamRepository
{
    Task<MissionTeamModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IEnumerable<MissionTeamModel>> GetByMissionIdAsync(int missionId, CancellationToken cancellationToken = default);
    Task<int> CreateAsync(MissionTeamModel model, CancellationToken cancellationToken = default);
    Task UpdateStatusAsync(int id, string status, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);

    Task<IEnumerable<MissionTeamModel>> GetActiveByRescuerTeamIdAsync(int rescuerTeamId, CancellationToken cancellationToken = default);
    Task<MissionTeamModel?> GetByMissionAndTeamAsync(int missionId, int rescuerTeamId, CancellationToken cancellationToken = default);
}
