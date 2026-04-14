using MediatR;
using RESQ.Application.Common.Models;

namespace RESQ.Application.UseCases.Logistics.Queries.GetMyReturnHistoryActivities;

public record GetMyReturnHistoryActivitiesQuery(Guid UserId) : IRequest<PagedResult<ReturnHistoryActivityDto>>
{
    public DateOnly? FromDate { get; init; }
    public DateOnly? ToDate { get; init; }
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
