using MediatR;
using RESQ.Application.Common.Models;

namespace RESQ.Application.UseCases.Logistics.Queries.GetMyPickupHistoryActivities;

public record GetMyPickupHistoryActivitiesQuery(Guid UserId) : IRequest<PagedResult<PickupHistoryActivityDto>>
{
    public DateOnly? FromDate { get; init; }
    public DateOnly? ToDate { get; init; }
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
