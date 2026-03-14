using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.UseCases.Identity.Queries.GetUsers;

namespace RESQ.Application.UseCases.Identity.Queries.GetRescuers;

public record GetRescuersQuery(
    int PageNumber,
    int PageSize,
    bool? IsBanned,
    bool? IsEligible,
    string? Search
) : IRequest<PagedResult<GetUsersItemResponse>>;
