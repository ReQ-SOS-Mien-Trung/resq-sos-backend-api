using RESQ.Application.Common.Logistics;
using RESQ.Application.Common.Models;

namespace RESQ.Application.Repositories.Logistics;

public interface IAdminActivityRepository
{
    Task<PagedResult<AdminActivityListItem>> GetPagedAllAsync(
        string? activityType,
        int? depotId,
        List<string>? statuses,
        DateOnly? fromDate,
        DateOnly? toDate,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);
}
