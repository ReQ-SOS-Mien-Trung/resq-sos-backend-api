using MediatR;
using RESQ.Application.Common.Models;

namespace RESQ.Application.UseCases.SystemConfig.Queries.GetAdminTeamList;

public record GetAdminTeamListQuery(int PageNumber = 1, int PageSize = 10)
    : IRequest<PagedResult<AdminTeamListItemDto>>;
