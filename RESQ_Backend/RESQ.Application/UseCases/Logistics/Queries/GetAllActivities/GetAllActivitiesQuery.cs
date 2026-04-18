using MediatR;
using RESQ.Application.Common.Models;

namespace RESQ.Application.UseCases.Logistics.Queries.GetAllActivities;

public record GetAllActivitiesQuery : IRequest<PagedResult<AdminActivityDto>>
{
    public string? ActivityType { get; init; }
    public int? DepotId { get; init; }
    public List<string>? Statuses { get; init; }
    public DateOnly? FromDate { get; init; }
    public DateOnly? ToDate { get; init; }
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
