using MediatR;

namespace RESQ.Application.UseCases.Operations.Queries.GetMissionTeams;

public record GetMissionTeamsQuery(int MissionId) : IRequest<GetMissionTeamsResponse>;
