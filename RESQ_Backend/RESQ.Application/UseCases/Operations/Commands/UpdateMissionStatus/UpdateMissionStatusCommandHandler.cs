using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common.StateMachines;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.Repositories.Personnel;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Operations.Shared;
using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Enum.Emergency;
using RESQ.Domain.Enum.Operations;
using RESQ.Domain.Enum.Personnel;

namespace RESQ.Application.UseCases.Operations.Commands.UpdateMissionStatus;

public class UpdateMissionStatusCommandHandler(
    IMissionRepository missionRepository,
    IMissionActivityRepository missionActivityRepository,
    IMissionTeamRepository missionTeamRepository,
    ISosRequestRepository sosRequestRepository,
    IUnitOfWork unitOfWork,
    ILogger<UpdateMissionStatusCommandHandler> logger,
    IAssemblyEventRepository assemblyEventRepository,
    IAdminRealtimeHubService adminRealtimeHubService,
    IRescueTeamMissionLifecycleSyncService rescueTeamMissionLifecycleSyncService
) : IRequestHandler<UpdateMissionStatusCommand, UpdateMissionStatusResponse>
{
    private readonly IMissionRepository _missionRepository = missionRepository;
    private readonly IMissionActivityRepository _missionActivityRepository = missionActivityRepository;
    private readonly IMissionTeamRepository _missionTeamRepository = missionTeamRepository;
    private readonly ISosRequestRepository _sosRequestRepository = sosRequestRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<UpdateMissionStatusCommandHandler> _logger = logger;
    private readonly IAssemblyEventRepository _assemblyEventRepository = assemblyEventRepository;
    private readonly IAdminRealtimeHubService _adminRealtimeHubService = adminRealtimeHubService;
    private readonly IRescueTeamMissionLifecycleSyncService _rescueTeamMissionLifecycleSyncService = rescueTeamMissionLifecycleSyncService;

    public async Task<UpdateMissionStatusResponse> Handle(UpdateMissionStatusCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Updating status for MissionId={missionId} -> {status}", request.MissionId, request.Status);

        var mission = await _missionRepository.GetByIdAsync(request.MissionId, cancellationToken);
        if (mission is null)
            throw new NotFoundException($"Không tìm thấy mission với ID: {request.MissionId}");

        if (request.Status is MissionStatus.Completed or MissionStatus.Incompleted)
            throw new BadRequestException("Mission chỉ được chốt sau khi các đội cứu hộ nộp báo cáo cuối cùng.");

        MissionStateMachine.EnsureValidTransition(mission.Status, request.Status);

        bool isCompleted = request.Status == MissionStatus.Completed || request.Status == MissionStatus.Incompleted;
        await _missionRepository.UpdateStatusAsync(request.MissionId, request.Status, isCompleted, cancellationToken);

        var rescueTeamLifecycleSyncResult = RescueTeamMissionLifecycleSyncResult.None;

        // Side-effects: update SOS requests in the cluster
        if (mission.ClusterId.HasValue)
        {
            if (request.Status == MissionStatus.OnGoing)
            {
                await _sosRequestRepository.UpdateStatusByClusterIdAsync(mission.ClusterId.Value, SosRequestStatus.InProgress, cancellationToken);
            }
        }

        if (request.Status == MissionStatus.OnGoing)
        {
            var missionTeams = (await _missionTeamRepository.GetByMissionIdAsync(request.MissionId, cancellationToken)).ToList();

            rescueTeamLifecycleSyncResult = await _rescueTeamMissionLifecycleSyncService.SyncTeamsToOnMissionAsync(
                missionTeams.Select(team => team.RescuerTeamId),
                cancellationToken);

            var missionActivities = await _missionActivityRepository.GetByMissionIdAsync(request.MissionId, cancellationToken);
            mission.Activities = missionActivities.ToList(); // Ensure activities are attached for Initialize
            foreach (var team in missionTeams)
            {
                MissionTeamSafetyHelper.InitializeSafetyTimeout(team, mission);
            }

            var autoStartedActivityIds = await MissionActivityAutoStartHelper.AutoStartFirstActivitiesPerTeamAsync(
                request.MissionId,
                request.DecisionBy,
                _missionActivityRepository,
                _missionTeamRepository,
                _logger,
                cancellationToken);

            if (autoStartedActivityIds.Count > 0)
            {
                _logger.LogInformation(
                    "MissionId={MissionId} auto-started {Count} first team activities: [{ActivityIds}]",
                    request.MissionId,
                    autoStartedActivityIds.Count,
                    string.Join(", ", autoStartedActivityIds));
            }

            await AutoCheckOutMissionTeamsAsync(request.MissionId, missionTeams, cancellationToken);
        }

        if (request.Status is MissionStatus.Completed or MissionStatus.Incompleted)
        {
            var missionTeams = await _missionTeamRepository.GetByMissionIdAsync(request.MissionId, cancellationToken);
            foreach (var team in missionTeams)
            {
                team.SafetyStatus = "Inactive";
                team.SafetyTimeoutAt = null;
            }
        }

        await _unitOfWork.SaveAsync();
        await _rescueTeamMissionLifecycleSyncService.PushRealtimeIfNeededAsync(
            rescueTeamLifecycleSyncResult,
            cancellationToken);
        await _adminRealtimeHubService.PushMissionUpdateAsync(
            new RESQ.Application.Common.Models.AdminMissionRealtimeUpdate
            {
                EntityId = mission.Id,
                EntityType = "Mission",
                MissionId = mission.Id,
                ClusterId = mission.ClusterId,
                Action = "StatusChanged",
                Status = request.Status.ToString(),
                ChangedAt = DateTime.UtcNow
            },
            cancellationToken);

        return new UpdateMissionStatusResponse
        {
            MissionId = request.MissionId,
            Status = request.Status.ToString(),
            IsCompleted = isCompleted
        };
    }

    /// <summary>
    /// Tự động checkout tất cả thành viên của các team thuộc mission khỏi sự kiện tập kết đang hoạt động.
    /// Gọi khi mission chuyển sang OnGoing (đội xuất phát làm nhiệm vụ).
    /// </summary>
    private async Task AutoCheckOutMissionTeamsAsync(
        int missionId,
        IReadOnlyCollection<MissionTeamModel> missionTeams,
        CancellationToken cancellationToken)
    {
        try
        {
            foreach (var team in missionTeams)
            {
                if (!team.AssemblyPointId.HasValue) continue;

                var activeEvent = await _assemblyEventRepository.GetActiveEventByAssemblyPointAsync(
                    team.AssemblyPointId.Value, cancellationToken);

                if (activeEvent is null) continue;

                var eventId = activeEvent.Value.EventId;
                var acceptedMembers = team.RescueTeamMembers
                    .Where(m => string.Equals(m.Status, TeamMemberStatus.Accepted.ToString(), StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var member in acceptedMembers)
                {
                    try
                    {
                        await _assemblyEventRepository.CheckOutAsync(eventId, member.UserId, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex,
                            "Auto-checkout failed for UserId={UserId} EventId={EventId} MissionId={MissionId}",
                            member.UserId, eventId, missionId);
                    }
                }

                _logger.LogInformation(
                    "Auto-checkout MissionId={MissionId} TeamId={TeamId} EventId={EventId}: {Count} member(s) checked out",
                    missionId, team.Id, eventId, acceptedMembers.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Auto-checkout failed for MissionId={MissionId}. Mission flow continues.", missionId);
        }
    }
}
