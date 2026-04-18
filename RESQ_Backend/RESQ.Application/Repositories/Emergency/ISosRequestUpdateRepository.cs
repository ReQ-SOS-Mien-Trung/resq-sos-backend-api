using RESQ.Domain.Entities.Emergency;

namespace RESQ.Application.Repositories.Emergency;

public interface ISosRequestUpdateRepository
{
    Task AddVictimUpdateAsync(SosRequestVictimUpdateModel update, CancellationToken cancellationToken = default);
    Task AddIncidentRangeAsync(IEnumerable<SosRequestIncidentUpdateModel> updates, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<int, IReadOnlyCollection<int>>> GetSosRequestIdsByTeamIncidentIdsAsync(
        IEnumerable<int> teamIncidentIds,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<int, IReadOnlyCollection<int>>> GetTeamIncidentIdsBySosRequestIdsAsync(
        IEnumerable<int> sosRequestIds,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<int, SosRequestVictimUpdateModel>> GetLatestVictimUpdatesBySosRequestIdsAsync(
        IEnumerable<int> sosRequestIds,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<int, IReadOnlyList<SosRequestIncidentUpdateModel>>> GetIncidentHistoryBySosRequestIdsAsync(
        IEnumerable<int> sosRequestIds,
        CancellationToken cancellationToken = default);
}
