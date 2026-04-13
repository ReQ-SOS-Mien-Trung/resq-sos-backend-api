using MediatR;
using RESQ.Application.Repositories.System;

namespace RESQ.Application.UseCases.SystemConfig.Commands.UpsertRescuerScoreVisibilityConfig;

public class UpsertRescuerScoreVisibilityConfigCommandHandler(
    IRescuerScoreVisibilityConfigRepository rescuerScoreVisibilityConfigRepository)
    : IRequestHandler<UpsertRescuerScoreVisibilityConfigCommand, UpsertRescuerScoreVisibilityConfigResponse>
{
    private readonly IRescuerScoreVisibilityConfigRepository _rescuerScoreVisibilityConfigRepository = rescuerScoreVisibilityConfigRepository;

    public async Task<UpsertRescuerScoreVisibilityConfigResponse> Handle(
        UpsertRescuerScoreVisibilityConfigCommand request,
        CancellationToken cancellationToken)
    {
        var saved = await _rescuerScoreVisibilityConfigRepository.UpsertAsync(
            request.MinimumEvaluationCount,
            request.UserId,
            cancellationToken);

        return new UpsertRescuerScoreVisibilityConfigResponse
        {
            MinimumEvaluationCount = saved.MinimumEvaluationCount,
            UpdatedBy = saved.UpdatedBy,
            UpdatedAt = saved.UpdatedAt,
            Message = "Cập nhật ngưỡng hiển thị điểm rescuer thành công."
        };
    }
}
