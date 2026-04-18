using MediatR;

namespace RESQ.Application.UseCases.SystemConfig.Queries.GetRescuerScoreVisibilityConfig;

public record GetRescuerScoreVisibilityConfigQuery : IRequest<GetRescuerScoreVisibilityConfigResponse>;
