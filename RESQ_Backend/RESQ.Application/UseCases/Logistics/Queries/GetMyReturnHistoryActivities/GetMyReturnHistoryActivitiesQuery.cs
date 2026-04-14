using MediatR;
using RESQ.Application.Common.Models;

namespace RESQ.Application.UseCases.Logistics.Queries.GetMyReturnHistoryActivities;

public record GetMyReturnHistoryActivitiesQuery(Guid UserId, int? DepotId = null) : IRequest<PagedResult<ReturnHistoryActivityDto>>
{
    public DateOnly? FromDate { get; init; }
    public DateOnly? ToDate { get; init; }
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
