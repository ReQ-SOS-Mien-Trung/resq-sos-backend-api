using MediatR;

namespace RESQ.Application.UseCases.SystemConfig.Queries.GetRescuerMissionScores;

public record GetRescuerMissionScoresQuery(Guid RescuerId) : IRequest<RescuerMissionScoresDto>;
