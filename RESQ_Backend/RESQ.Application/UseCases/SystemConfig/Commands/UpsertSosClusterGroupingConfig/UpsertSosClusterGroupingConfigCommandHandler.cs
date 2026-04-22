using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.System;
using RESQ.Application.Services;

namespace RESQ.Application.UseCases.SystemConfig.Commands.UpsertSosClusterGroupingConfig;

public class UpsertSosClusterGroupingConfigCommandHandler(
    ISosClusterGroupingConfigRepository sosClusterGroupingConfigRepository,
    IAdminRealtimeHubService adminRealtimeHubService)
    : IRequestHandler<UpsertSosClusterGroupingConfigCommand, UpsertSosClusterGroupingConfigResponse>
{
    private readonly ISosClusterGroupingConfigRepository _sosClusterGroupingConfigRepository = sosClusterGroupingConfigRepository;
    private readonly IAdminRealtimeHubService _adminRealtimeHubService = adminRealtimeHubService;

    public async Task<UpsertSosClusterGroupingConfigResponse> Handle(
        UpsertSosClusterGroupingConfigCommand request,
        CancellationToken cancellationToken)
    {
        var saved = await _sosClusterGroupingConfigRepository.UpsertAsync(
            request.MaximumDistanceKm,
            request.UserId,
            cancellationToken);

        var response = new UpsertSosClusterGroupingConfigResponse
        {
            MaximumDistanceKm = saved.MaximumDistanceKm,
            UpdatedBy = saved.UpdatedBy,
            UpdatedAt = saved.UpdatedAt,
            Message = "Cập nhật khoảng cách gom cluster thành công."
        };

        await _adminRealtimeHubService.PushSystemConfigUpdateAsync(new AdminSystemConfigRealtimeUpdate
        {
            EntityType = "SosClusterGroupingConfig",
            ConfigKey = "sos-cluster-grouping",
            Action = "Updated",
            Status = "Updated",
            ChangedAt = response.UpdatedAt
        }, cancellationToken);

        return response;
    }
}
