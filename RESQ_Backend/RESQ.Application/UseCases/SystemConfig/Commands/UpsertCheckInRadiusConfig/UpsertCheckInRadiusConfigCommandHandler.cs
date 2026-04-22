using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.System;
using RESQ.Application.Services;

namespace RESQ.Application.UseCases.SystemConfig.Commands.UpsertCheckInRadiusConfig;

public class UpsertCheckInRadiusConfigCommandHandler(
    ICheckInRadiusConfigRepository checkInRadiusConfigRepository,
    IAdminRealtimeHubService adminRealtimeHubService)
    : IRequestHandler<UpsertCheckInRadiusConfigCommand, UpsertCheckInRadiusConfigResponse>
{
    private readonly ICheckInRadiusConfigRepository _checkInRadiusConfigRepository = checkInRadiusConfigRepository;
    private readonly IAdminRealtimeHubService _adminRealtimeHubService = adminRealtimeHubService;

    public async Task<UpsertCheckInRadiusConfigResponse> Handle(
        UpsertCheckInRadiusConfigCommand request,
        CancellationToken cancellationToken)
    {
        var saved = await _checkInRadiusConfigRepository.UpsertAsync(
            request.MaxRadiusMeters,
            request.UserId,
            cancellationToken);

        var response = new UpsertCheckInRadiusConfigResponse
        {
            MaxRadiusMeters = saved.MaxRadiusMeters,
            UpdatedBy = saved.UpdatedBy,
            UpdatedAt = saved.UpdatedAt,
            Message = "Cập nhật bán kính check-in thành công."
        };

        await _adminRealtimeHubService.PushSystemConfigUpdateAsync(new AdminSystemConfigRealtimeUpdate
        {
            EntityType = "CheckInRadiusConfig",
            ConfigKey = "check-in-radius",
            Action = "Updated",
            Status = "Updated",
            ChangedAt = response.UpdatedAt
        }, cancellationToken);

        return response;
    }
}
