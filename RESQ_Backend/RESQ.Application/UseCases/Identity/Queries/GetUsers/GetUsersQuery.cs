using MediatR;
using RESQ.Application.Common.Models;

namespace RESQ.Application.UseCases.Identity.Queries.GetUsers;

public record GetUsersQuery(
    int PageNumber,
    int PageSize,
    int? RoleId,
    bool? IsBanned,
    string? Search
) : IRequest<PagedResult<GetUsersItemResponse>>;
