using MediatR;
using RESQ.Application.UseCases.Operations.Queries.GetMissions;

namespace RESQ.Application.UseCases.Operations.Queries.GetMissionById;

public record GetMissionByIdQuery(int MissionId) : IRequest<MissionDto?>;
