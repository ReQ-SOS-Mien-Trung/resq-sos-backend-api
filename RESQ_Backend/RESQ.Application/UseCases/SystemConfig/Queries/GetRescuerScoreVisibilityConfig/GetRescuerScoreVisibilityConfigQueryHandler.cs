using MediatR;
using RESQ.Application.Repositories.System;

namespace RESQ.Application.UseCases.SystemConfig.Queries.GetRescuerScoreVisibilityConfig;

public class GetRescuerScoreVisibilityConfigQueryHandler(
    IRescuerScoreVisibilityConfigRepository rescuerScoreVisibilityConfigRepository)
    : IRequestHandler<GetRescuerScoreVisibilityConfigQuery, GetRescuerScoreVisibilityConfigResponse>
{
    private readonly IRescuerScoreVisibilityConfigRepository _rescuerScoreVisibilityConfigRepository = rescuerScoreVisibilityConfigRepository;

    public async Task<GetRescuerScoreVisibilityConfigResponse> Handle(
        GetRescuerScoreVisibilityConfigQuery request,
        CancellationToken cancellationToken)
    {
        var config = await _rescuerScoreVisibilityConfigRepository.GetAsync(cancellationToken);

        return new GetRescuerScoreVisibilityConfigResponse
        {
            MinimumEvaluationCount = config?.MinimumEvaluationCount ?? 0,
            UpdatedBy = config?.UpdatedBy,
            UpdatedAt = config?.UpdatedAt ?? DateTime.UtcNow
        };
    }
}