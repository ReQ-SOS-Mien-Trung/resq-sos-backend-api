using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common.Models;
using RESQ.Application.Services;
using RESQ.Application.Repositories.Personnel;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.UseCases.SystemConfig.Queries.GetVictimsByPeriod;
using RESQ.Presentation.Hubs;

namespace RESQ.Presentation.Services;

public class DashboardHubService(
    IHubContext<DashboardHub> hubContext,
    IMediator mediator,
    IAssemblyPointRepository assemblyPointRepository,
    IAssemblyEventRepository assemblyEventRepository,
    IMissionActivityRepository missionActivityRepository,
    ILogger<DashboardHubService> logger
) : IDashboardHubService
{
    private const string GroupName = "admin_dashboard";
    private readonly IHubContext<DashboardHub> _hubContext = hubContext;
    private readonly IMediator _mediator = mediator;
    private readonly IAssemblyPointRepository _assemblyPointRepository = assemblyPointRepository;
    private readonly IAssemblyEventRepository _assemblyEventRepository = assemblyEventRepository;
    private readonly IMissionActivityRepository _missionActivityRepository = missionActivityRepository;
    private readonly ILogger<DashboardHubService> _logger = logger;

    /// <inheritdoc/>
    public async Task PushVictimsByPeriodAsync(CancellationToken cancellationToken = default)
    {
        // Push default view: 6 tháng gần nhất, group by month
        var data = await _mediator.Send(
            new GetVictimsByPeriodQuery(null, null, null),
            cancellationToken);

        await _hubContext.Clients.Group(GroupName)
            .SendAsync("ReceiveVictimsByPeriod", data, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task PushAssemblyPointSnapshotAsync(int assemblyPointId, string operation, CancellationToken cancellationToken = default)
    {
        try
        {
            var assemblyPoint = await _assemblyPointRepository.GetByIdAsync(assemblyPointId, cancellationToken);
            if (assemblyPoint is null)
            {
                _logger.LogWarning("Skip assembly point realtime push because assembly point {AssemblyPointId} was not found.", assemblyPointId);
                return;
            }

            var activeEvent = await _assemblyEventRepository.GetActiveEventByAssemblyPointAsync(assemblyPointId, cancellationToken);
            var openActivities = await _missionActivityRepository.GetOpenByAssemblyPointAsync(assemblyPointId, cancellationToken);

            var requiresReroute = string.Equals(assemblyPoint.Status.ToString(), "Unavailable", StringComparison.OrdinalIgnoreCase)
                                   && openActivities.Count > 0;

            var payload = new AssemblyPointRealtimeSnapshotDto
            {
                AssemblyPointId = assemblyPoint.Id,
                Code = assemblyPoint.Code,
                Name = assemblyPoint.Name,
                Status = assemblyPoint.Status.ToString(),
                HasActiveEvent = activeEvent is not null,
                ActiveMissionActivityCount = openActivities.Count,
                RequiresReroute = requiresReroute,
                AlertMessage = requiresReroute
                    ? $"Cảnh báo: Điểm tập kết {assemblyPoint.Name} đang Unavailable nhưng vẫn có {openActivities.Count} mission activity đang hướng về đây. Điều phối cần reroute ngay."
                    : null,
                RecommendedAction = requiresReroute
                    ? "Reroute active teams to another assembly point."
                    : "No reroute required.",
                OccurredAtUtc = DateTime.UtcNow,
                Operation = operation,
                ActiveMissionActivities = openActivities.Select(activity => new AssemblyPointRealtimeMissionActivityDto
                {
                    Id = activity.Id,
                    MissionId = activity.MissionId,
                    Step = activity.Step,
                    ActivityType = activity.ActivityType,
                    Status = activity.Status.ToString(),
                    MissionTeamId = activity.MissionTeamId,
                    SosRequestId = activity.SosRequestId,
                    AssemblyPointId = activity.AssemblyPointId,
                    AssemblyPointName = activity.AssemblyPointName
                }).ToList()
            };

            await _hubContext.Clients.Group(GroupName)
                .SendAsync("ReceiveAssemblyPointSnapshot", payload, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to push assembly point realtime snapshot for AssemblyPointId={AssemblyPointId}", assemblyPointId);
        }
    }
}
