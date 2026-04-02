using MediatR;
using RESQ.Application.UseCases.Operations.Queries.GetMissions;

namespace RESQ.Application.UseCases.Operations.Queries.GetMyTeamActivities;

public record GetMyTeamActivitiesQuery(int MissionId, Guid UserId) : IRequest<List<MissionActivityDto>>;
