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

        var sosTasks = ids.ToDictionary(
            id => id,
            id => sosRequestRepository.GetByIdAsync(id, cancellationToken));

        await Task.WhenAll(sosTasks.Values);

        var sosRequests = sosTasks.Values
            .Select(task => task.Result)
            .Where(request => request is not null)
            .Select(request => request!)
            .ToList();

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
