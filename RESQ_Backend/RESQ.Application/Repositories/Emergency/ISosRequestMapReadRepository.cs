using RESQ.Application.Common.Sorting;
using RESQ.Domain.Entities.Emergency;
using RESQ.Domain.Enum.Emergency;

namespace RESQ.Application.Repositories.Emergency;

public interface ISosRequestMapReadRepository
{
    Task<List<SosRequestModel>> GetByBoundsAsync(
        double minLat,
        double maxLat,
        double minLng,
        double maxLng,
        IReadOnlyCollection<SosRequestStatus>? statuses = null,
        IReadOnlyCollection<SosPriorityLevel>? priorities = null,
        IReadOnlyCollection<SosRequestType>? sosTypes = null,
        IReadOnlyList<SosSortOption>? sortOptions = null,
        CancellationToken cancellationToken = default);
}
