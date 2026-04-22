using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.System;
using RESQ.Application.Services;

namespace RESQ.Application.UseCases.SystemConfig.Commands.UpsertRescueTeamRadiusConfig;

public class UpsertRescueTeamRadiusConfigCommandHandler(
    IRescueTeamRadiusConfigRepository rescueTeamRadiusConfigRepository,
    IAdminRealtimeHubService adminRealtimeHubService)
    : IRequestHandler<UpsertRescueTeamRadiusConfigCommand, UpsertRescueTeamRadiusConfigResponse>
{
    private readonly IRescueTeamRadiusConfigRepository _rescueTeamRadiusConfigRepository = rescueTeamRadiusConfigRepository;
    private readonly IAdminRealtimeHubService _adminRealtimeHubService = adminRealtimeHubService;

    public async Task<UpsertRescueTeamRadiusConfigResponse> Handle(
        UpsertRescueTeamRadiusConfigCommand request,
        CancellationToken cancellationToken)
    {
        var saved = await _rescueTeamRadiusConfigRepository.UpsertAsync(
            request.MaxRadiusKm,
            request.UserId,
            cancellationToken);

        var response = new UpsertRescueTeamRadiusConfigResponse
        {
            MaxRadiusKm = saved.MaxRadiusKm,
            UpdatedBy = saved.UpdatedBy,
            UpdatedAt = saved.UpdatedAt,
            Message = "Cập nhật bán kính tìm kiếm đội cứu hộ thành công."
        };

        await _adminRealtimeHubService.PushSystemConfigUpdateAsync(new AdminSystemConfigRealtimeUpdate
        {
            EntityType = "RescueTeamRadiusConfig",
            ConfigKey = "rescue-team-radius",
            Action = "Updated",
            Status = "Updated",
            ChangedAt = response.UpdatedAt
        }, cancellationToken);

        return response;
    }
}
