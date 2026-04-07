using RESQ.Domain.Entities.Emergency;

namespace RESQ.Application.Repositories.Emergency;

public interface ISosRequestUpdateRepository
{
    Task AddIncidentRangeAsync(IEnumerable<SosRequestIncidentUpdateModel> updates, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<int, IReadOnlyList<SosRequestIncidentUpdateModel>>> GetIncidentHistoryBySosRequestIdsAsync(
        IEnumerable<int> sosRequestIds,
        CancellationToken cancellationToken = default);
}