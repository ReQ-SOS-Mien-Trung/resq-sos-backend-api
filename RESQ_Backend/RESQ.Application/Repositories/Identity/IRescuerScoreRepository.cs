using RESQ.Domain.Entities.Identity;
using RESQ.Domain.Entities.Operations;

namespace RESQ.Application.Repositories.Identity;

public interface IRescuerScoreRepository
{
    Task<RescuerScoreModel?> GetByRescuerIdAsync(Guid rescuerId, CancellationToken cancellationToken = default);
    Task<IDictionary<Guid, RescuerScoreModel>> GetByRescuerIdsAsync(IEnumerable<Guid> rescuerIds, CancellationToken cancellationToken = default);
    Task RefreshAsync(IEnumerable<MissionTeamMemberEvaluationModel> newEvaluations, CancellationToken cancellationToken = default);
}
