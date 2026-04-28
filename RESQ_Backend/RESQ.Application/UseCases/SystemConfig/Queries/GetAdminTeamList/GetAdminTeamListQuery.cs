using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Domain.Enum.Personnel;

namespace RESQ.Application.UseCases.SystemConfig.Queries.GetAdminTeamList;

public record GetAdminTeamListQuery(
    int PageNumber = 1,
    int PageSize = 10,
    RescueTeamType? TeamType = null,
    RescueTeamStatus? Status = null,
    string? AssemblyPointName = null,
    string? Search = null)
    : IRequest<PagedResult<AdminTeamListItemDto>>;
