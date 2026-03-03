using MediatR;
using RESQ.Application.UseCases.Operations.Queries.GetMissions;

namespace RESQ.Application.UseCases.Operations.Queries.GetMissionActivities;

public record GetMissionActivitiesQuery(int MissionId) : IRequest<List<MissionActivityDto>>;
