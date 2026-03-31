using MediatR;
using RESQ.Application.Common.Models;

namespace RESQ.Application.UseCases.Logistics.Queries.GetMyUpcomingPickupActivities;

public record GetMyUpcomingPickupActivitiesQuery(Guid UserId) : IRequest<PagedResult<UpcomingPickupActivityDto>>
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
