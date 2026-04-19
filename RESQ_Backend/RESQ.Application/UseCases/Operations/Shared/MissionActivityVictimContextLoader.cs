using RESQ.Application.Common;
using RESQ.Application.Repositories.Emergency;

namespace RESQ.Application.UseCases.Operations.Shared;

public static class MissionActivityVictimContextLoader
{
    public static async Task<IReadOnlyDictionary<int, MissionActivityVictimContext>> LoadAsync(
        IEnumerable<int> sosRequestIds,
        ISosRequestRepository sosRequestRepository,
        ISosRequestUpdateRepository sosRequestUpdateRepository,
        CancellationToken cancellationToken = default)
    {
        var ids = sosRequestIds
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        if (ids.Count == 0)
            return new Dictionary<int, MissionActivityVictimContext>();

        var sosRequests = new List<RESQ.Domain.Entities.Emergency.SosRequestModel>();
        var loadedSosIds = new HashSet<int>();

        if (sosRequestRepository is ISosRequestBulkReadRepository bulkReadRepository)
        {
            var bulkRequests = await bulkReadRepository.GetByIdsAsync(ids, cancellationToken);
            foreach (var request in bulkRequests)
            {
                if (!loadedSosIds.Add(request.Id))
                {
                    continue;
                }

                sosRequests.Add(request);
            }
        }

        if (loadedSosIds.Count < ids.Count)
        {
            foreach (var id in ids)
            {
                if (loadedSosIds.Contains(id))
                {
                    continue;
                }

                var request = await sosRequestRepository.GetByIdAsync(id, cancellationToken);
                if (request is null || !loadedSosIds.Add(request.Id))
                {
                    continue;
                }

                sosRequests.Add(request);
            }
        }

        if (sosRequests.Count == 0)
            return new Dictionary<int, MissionActivityVictimContext>();

        var latestVictimUpdates = await sosRequestUpdateRepository.GetLatestVictimUpdatesBySosRequestIdsAsync(
            sosRequests.Select(request => request.Id),
            cancellationToken);

        return sosRequests.ToDictionary(
            request => request.Id,
            request =>
            {
                latestVictimUpdates.TryGetValue(request.Id, out var latestVictimUpdate);
                var effectiveRequest = SosRequestVictimUpdateOverlay.Apply(request, latestVictimUpdate);
                return MissionActivityVictimContextHelper.BuildContext(
                    effectiveRequest.StructuredData,
                    effectiveRequest.Id);
            });
    }
}
