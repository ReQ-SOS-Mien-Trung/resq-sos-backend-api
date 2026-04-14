using RESQ.Application.Common.Logistics;
using RESQ.Application.Common.Models;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Application.Repositories.Logistics;

public interface IReturnSupplyActivityRepository
{
    Task<PagedResult<UpcomingReturnActivityListItem>> GetPagedByDepotIdAsync(
        int depotId,
        MissionActivityStatus status,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<PagedResult<ReturnHistoryActivityListItem>> GetHistoryPagedByDepotIdAsync(
        int depotId,
        DateOnly? fromDate,
        DateOnly? toDate,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);
}
