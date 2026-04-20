using RESQ.Domain.Entities.System;

namespace RESQ.Application.Repositories.System;

public sealed record ServiceZoneResourceCounts(
    int ServiceZoneId,
    int PendingSosRequestCount,
    int IncidentSosRequestCount,
    int TeamIncidentCount,
    int AssemblyPointCount,
    int DepotCount);

public interface IServiceZoneSummaryRepository
{
    Task<IReadOnlyDictionary<int, ServiceZoneResourceCounts>> GetResourceCountsAsync(
        IReadOnlyCollection<ServiceZoneModel> serviceZones,
        CancellationToken cancellationToken = default);
}
