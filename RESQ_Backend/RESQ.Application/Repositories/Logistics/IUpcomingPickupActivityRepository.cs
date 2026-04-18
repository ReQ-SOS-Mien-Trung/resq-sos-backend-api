using RESQ.Application.Common.Logistics;
using RESQ.Application.Common.Models;

namespace RESQ.Application.Repositories.Logistics;

public interface IUpcomingPickupActivityRepository
{
    Task<PagedResult<UpcomingPickupActivityListItem>> GetPagedByDepotIdAsync(
        int depotId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<PagedResult<PickupHistoryActivityListItem>> GetHistoryPagedByDepotIdAsync(
        int depotId,
        DateOnly? fromDate,
        DateOnly? toDate,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);
}
