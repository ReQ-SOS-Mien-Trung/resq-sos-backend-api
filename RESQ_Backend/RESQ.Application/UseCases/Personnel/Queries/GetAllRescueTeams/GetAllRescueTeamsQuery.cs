using MediatR;
using RESQ.Application.Common.Models;

namespace RESQ.Application.UseCases.Personnel.Queries.GetAllRescueTeams;

public record GetAllRescueTeamsQuery(int PageNumber = 1, int PageSize = 10) : IRequest<PagedResult<RescueTeamDto>>;
