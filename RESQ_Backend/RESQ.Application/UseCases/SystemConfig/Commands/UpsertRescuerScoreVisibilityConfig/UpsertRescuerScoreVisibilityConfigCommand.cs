using MediatR;

namespace RESQ.Application.UseCases.SystemConfig.Commands.UpsertRescuerScoreVisibilityConfig;

public class UpsertRescuerScoreVisibilityConfigCommand : IRequest<UpsertRescuerScoreVisibilityConfigResponse>
{
    public Guid UserId { get; set; }
    public int MinimumEvaluationCount { get; set; }
}
