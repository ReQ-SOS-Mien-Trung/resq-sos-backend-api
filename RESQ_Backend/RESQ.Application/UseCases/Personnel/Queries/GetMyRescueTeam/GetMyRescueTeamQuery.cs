using MediatR;
using RESQ.Application.UseCases.Personnel.Queries.GetAllRescueTeams;

namespace RESQ.Application.UseCases.Personnel.Queries.GetMyRescueTeam;

public record GetMyRescueTeamQuery(Guid UserId) : IRequest<RescueTeamDetailDto>;
