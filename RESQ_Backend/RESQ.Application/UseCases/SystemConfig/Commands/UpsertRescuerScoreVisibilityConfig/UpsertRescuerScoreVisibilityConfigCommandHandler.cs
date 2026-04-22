using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.System;
using RESQ.Application.Services;

namespace RESQ.Application.UseCases.SystemConfig.Commands.UpsertRescuerScoreVisibilityConfig;

public class UpsertRescuerScoreVisibilityConfigCommandHandler(
    IRescuerScoreVisibilityConfigRepository rescuerScoreVisibilityConfigRepository,
    IAdminRealtimeHubService adminRealtimeHubService)
    : IRequestHandler<UpsertRescuerScoreVisibilityConfigCommand, UpsertRescuerScoreVisibilityConfigResponse>
{
    private readonly IRescuerScoreVisibilityConfigRepository _rescuerScoreVisibilityConfigRepository = rescuerScoreVisibilityConfigRepository;
    private readonly IAdminRealtimeHubService _adminRealtimeHubService = adminRealtimeHubService;

    public async Task<UpsertRescuerScoreVisibilityConfigResponse> Handle(
        UpsertRescuerScoreVisibilityConfigCommand request,
        CancellationToken cancellationToken)
    {
        var saved = await _rescuerScoreVisibilityConfigRepository.UpsertAsync(
            request.MinimumEvaluationCount,
            request.UserId,
            cancellationToken);

        var response = new UpsertRescuerScoreVisibilityConfigResponse
        {
            MinimumEvaluationCount = saved.MinimumEvaluationCount,
            UpdatedBy = saved.UpdatedBy,
            UpdatedAt = saved.UpdatedAt,
            Message = "Cập nhật ngưỡng hiển thị điểm rescuer thành công."
        };

        await _adminRealtimeHubService.PushSystemConfigUpdateAsync(new AdminSystemConfigRealtimeUpdate
        {
            EntityType = "RescuerScoreVisibilityConfig",
            ConfigKey = "rescuer-score-visibility",
            Action = "Updated",
            Status = "Updated",
            ChangedAt = response.UpdatedAt
        }, cancellationToken);

        return response;
    }
}
