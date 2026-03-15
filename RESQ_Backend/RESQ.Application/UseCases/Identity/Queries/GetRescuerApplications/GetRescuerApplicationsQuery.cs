using MediatR;
using RESQ.Application.Common.Models;

namespace RESQ.Application.UseCases.Identity.Queries.GetRescuerApplications
{
    public record GetRescuerApplicationsQuery(
        int PageNumber = 1,
        int PageSize = 10,
        string? Status = null, // Filter by status: Pending, Approved, Rejected
        string? Name = null,
        string? Email = null,
        string? Phone = null,
        string? RescuerType = null // Filter by rescuer type: Core, Volunteer
    ) : IRequest<PagedResult<RescuerApplicationDto>>;
}
