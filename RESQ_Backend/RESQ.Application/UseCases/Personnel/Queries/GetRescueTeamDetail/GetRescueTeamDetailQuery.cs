using MediatR;
using RESQ.Application.UseCases.Personnel.Queries.GetAllRescueTeams;

namespace RESQ.Application.UseCases.Personnel.Queries.GetRescueTeamDetail;

public record GetRescueTeamDetailQuery(int Id) : IRequest<RescueTeamDetailDto>;