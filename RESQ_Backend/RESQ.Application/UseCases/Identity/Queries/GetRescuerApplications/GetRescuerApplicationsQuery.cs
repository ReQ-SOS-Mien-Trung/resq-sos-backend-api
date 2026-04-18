using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Domain.Enum.Identity;

namespace RESQ.Application.UseCases.Identity.Queries.GetRescuerApplications
{
    public record GetRescuerApplicationsQuery(
        int PageNumber = 1,
        int PageSize = 10,
        RescuerApplicationStatus? Status = null,
        string? Name = null,
        string? Email = null,
        string? Phone = null,
        string? RescuerType = null
    ) : IRequest<PagedResult<RescuerApplicationListItemDto>>;
}
