using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Application.UseCases.Logistics.Queries.GetMyUpcomingReturnActivities;

public record GetMyUpcomingReturnActivitiesQuery(Guid UserId) : IRequest<PagedResult<UpcomingReturnActivityDto>>
{
    public MissionActivityStatus Status { get; init; }
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}