using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.Repositories.Personnel;
using RESQ.Application.Services;
using RESQ.Domain.Enum.Personnel;

namespace RESQ.Application.UseCases.Personnel.Commands.SetAssemblyPointUnavailableWithReassignment;

public class SetAssemblyPointUnavailableWithReassignmentCommandHandler(
    IAssemblyPointRepository assemblyPointRepository,
    IAssemblyEventRepository assemblyEventRepository,
    IMissionActivityRepository missionActivityRepository,
    IRescueTeamRepository rescueTeamRepository,
    IUnitOfWork unitOfWork,
    IDashboardHubService dashboardHubService,
    IOperationalHubService operationalHubService,
    IFirebaseService firebaseService,
    ILogger<SetAssemblyPointUnavailableWithReassignmentCommandHandler> logger)
    : IRequestHandler<SetAssemblyPointUnavailableWithReassignmentCommand, SetAssemblyPointUnavailableWithReassignmentResponse>
{
    public async Task<SetAssemblyPointUnavailableWithReassignmentResponse> Handle(
        SetAssemblyPointUnavailableWithReassignmentCommand request,
        CancellationToken cancellationToken)
    {
        var notificationUserIds = new HashSet<Guid>();
        var reassignedRescuerCount = 0;
        var reassignedStationedTeamCount = 0;
        var reassignedActivityCount = 0;
        string finalStatus = AssemblyPointStatus.Unavailable.ToString();

        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var assemblyPoint = await assemblyPointRepository.GetByIdAsync(request.AssemblyPointId, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy điểm tập kết.");

            if (assemblyPoint.Status != AssemblyPointStatus.PendingUnavailable)
            {
                throw new ConflictException($"Điểm tập kết phải ở trạng thái PendingUnavailable trước khi lưu điều phối lại. Trạng thái hiện tại: {assemblyPoint.Status}.");
            }

            var currentCheckedIn = await assemblyPointRepository.GetCheckedInRescuersAsync(
                request.AssemblyPointId,
                cancellationToken);
            var currentCheckedInIds = currentCheckedIn.Select(x => x.UserId).ToHashSet();

            var currentStationedTeams = await rescueTeamRepository.GetAvailableStationedTeamsByAssemblyPointAsync(
                request.AssemblyPointId,
                cancellationToken);
            var currentStationedTeamIds = currentStationedTeams.Select(x => x.RescueTeamId).ToHashSet();

            var currentImpacts = await missionActivityRepository.GetReassignableAssemblyPointImpactsAsync(
                request.AssemblyPointId,
                cancellationToken);
            var currentActivities = currentImpacts.SelectMany(x => x.Activities).ToList();
            var currentActivityIds = currentActivities.Select(x => x.MissionActivityId).ToHashSet();

            var rescuerAssignments = BuildRescuerAssignments(request.RescuerReassignments);
            EnsureNoUnknownIds(rescuerAssignments.Keys, currentCheckedInIds, "stale checked-in rescuers");
            EnsureAllIdsAssigned(currentCheckedInIds, rescuerAssignments.Keys, "checked-in rescuers");

            var stationedTeamAssignments = BuildStationedTeamAssignments(request.TeamReassignments);
            EnsureNoUnknownIds(stationedTeamAssignments.Keys, currentStationedTeamIds, "stale stationed teams");
            EnsureAllIdsAssigned(currentStationedTeamIds, stationedTeamAssignments.Keys, "stationed teams");

            var activityAssignments = BuildActivityAssignments(request);
            EnsureNoUnknownIds(activityAssignments.Keys, currentActivityIds, "stale mission activities");
            EnsureAllIdsAssigned(currentActivityIds, activityAssignments.Keys, "mission activities");

            var targetAssemblyPointIds = rescuerAssignments.Values
                .Concat(stationedTeamAssignments.Values)
                .Concat(activityAssignments.Values)
                .Distinct()
                .ToList();
            await ValidateTargetsAvailableAsync(targetAssemblyPointIds, request.AssemblyPointId, assemblyPointRepository, cancellationToken);

            if (rescuerAssignments.Count > 0)
            {
                await assemblyPointRepository.BulkUpdateRescuerAssemblyPointMapAsync(rescuerAssignments, cancellationToken);
                await assemblyEventRepository.CheckOutCheckedInParticipantsAsync(
                    request.AssemblyPointId,
                    rescuerAssignments.Keys.ToList(),
                    cancellationToken);
                reassignedRescuerCount = rescuerAssignments.Count;
                notificationUserIds.UnionWith(rescuerAssignments.Keys);
            }

            if (stationedTeamAssignments.Count > 0)
            {
                var memberAssignments = BuildStationedTeamMemberAssignments(
                    currentStationedTeams,
                    stationedTeamAssignments,
                    rescuerAssignments);

                await rescueTeamRepository.ReassignAvailableStationedTeamsAsync(
                    request.AssemblyPointId,
                    stationedTeamAssignments,
                    cancellationToken);

                await assemblyPointRepository.BulkUpdateRescuerAssemblyPointMapAsync(
                    memberAssignments,
                    cancellationToken);

                await assemblyEventRepository.CheckOutCheckedInParticipantsAsync(
                    request.AssemblyPointId,
                    memberAssignments.Keys.ToList(),
                    cancellationToken);

                reassignedStationedTeamCount = stationedTeamAssignments.Count;
                notificationUserIds.UnionWith(memberAssignments.Keys);
            }

            var activeEvent = await assemblyEventRepository.GetActiveEventByAssemblyPointAsync(request.AssemblyPointId, cancellationToken);
            if (activeEvent != null)
            {
                var participantIds = await assemblyEventRepository.GetParticipantIdsAsync(activeEvent.Value.EventId, cancellationToken);
                notificationUserIds.UnionWith(participantIds);
                await assemblyEventRepository.CheckOutCheckedInParticipantsAsync(
                    request.AssemblyPointId,
                    participantIds,
                    cancellationToken);
                await assemblyEventRepository.UpdateEventStatusAsync(
                    activeEvent.Value.EventId,
                    AssemblyEventStatus.Cancelled.ToString(),
                    cancellationToken);
            }

            if (activityAssignments.Count > 0)
            {
                var changedActivityIds = await missionActivityRepository.ReassignAssemblyPointAsync(
                    request.AssemblyPointId,
                    activityAssignments,
                    request.ChangedBy,
                    cancellationToken);

                reassignedActivityCount = changedActivityIds.Count;

                var affectedTeamIds = currentActivities
                    .Where(x => changedActivityIds.Contains(x.MissionActivityId) && x.RescueTeamId.HasValue)
                    .Select(x => x.RescueTeamId!.Value)
                    .Distinct()
                    .ToList();

                var membersByTeam = await rescueTeamRepository.GetAcceptedMemberUserIdsByTeamIdsAsync(affectedTeamIds, cancellationToken);
                foreach (var userId in membersByTeam.Values.SelectMany(x => x))
                {
                    notificationUserIds.Add(userId);
                }
            }

            assemblyPoint.ChangeStatus(AssemblyPointStatus.Unavailable, request.ChangedBy, request.Reason);
            finalStatus = assemblyPoint.Status.ToString();
            await assemblyPointRepository.UpdateAsync(assemblyPoint, cancellationToken);
            await unitOfWork.SaveAsync();
        });

        await Task.WhenAll(
            dashboardHubService.PushAssemblyPointSnapshotAsync(request.AssemblyPointId, "StartMaintenance", cancellationToken),
            operationalHubService.PushAssemblyPointListUpdateAsync(cancellationToken));

        await NotifyAffectedUsersAsync(notificationUserIds, firebaseService, logger, cancellationToken);

        return new SetAssemblyPointUnavailableWithReassignmentResponse
        {
            AssemblyPointId = request.AssemblyPointId,
            Status = finalStatus,
            ReassignedRescuerCount = reassignedRescuerCount,
            ReassignedStationedTeamCount = reassignedStationedTeamCount,
            ReassignedMissionActivityCount = reassignedActivityCount,
            NotifiedUserCount = notificationUserIds.Count,
            Message = "Điều phối lại điểm tập kết hoàn tất."
        };
    }

    private static Dictionary<Guid, int> BuildRescuerAssignments(
        IReadOnlyList<RescuerAssemblyPointReassignmentDto> reassignments)
    {
        var result = new Dictionary<Guid, int>();
        foreach (var reassignment in reassignments)
        {
            if (result.TryGetValue(reassignment.UserId, out var existingTarget)
                && existingTarget != reassignment.TargetAssemblyPointId)
            {
                throw new BadRequestException($"Rescuer {reassignment.UserId} được gán tới nhiều điểm tập kết đích khác nhau.");
            }

            result[reassignment.UserId] = reassignment.TargetAssemblyPointId;
        }

        return result;
    }

    private static Dictionary<Guid, int> BuildStationedTeamMemberAssignments(
        IReadOnlyList<RESQ.Application.Common.Models.AssemblyPointUnavailableStationedTeamDto> currentStationedTeams,
        IReadOnlyDictionary<int, int> stationedTeamAssignments,
        IReadOnlyDictionary<Guid, int> explicitRescuerAssignments)
    {
        var result = new Dictionary<Guid, int>();

        foreach (var team in currentStationedTeams.Where(x => stationedTeamAssignments.ContainsKey(x.RescueTeamId)))
        {
            var targetAssemblyPointId = stationedTeamAssignments[team.RescueTeamId];
            foreach (var memberUserId in team.MemberUserIds)
            {
                if (explicitRescuerAssignments.TryGetValue(memberUserId, out var explicitTarget)
                    && explicitTarget != targetAssemblyPointId)
                {
                    throw new BadRequestException(
                        $"Nhân sự {memberUserId} đã được gán trực tiếp vào điểm tập kết #{explicitTarget}, nhưng đội đóng quân #{team.RescueTeamId} của họ lại được gán vào điểm tập kết #{targetAssemblyPointId}.");
                }

                if (result.TryGetValue(memberUserId, out var existingTarget)
                    && existingTarget != targetAssemblyPointId)
                {
                    throw new BadRequestException(
                        $"Nhân sự {memberUserId} thuộc nhiều đội đóng quân với điểm tập kết đích khác nhau.");
                }

                result[memberUserId] = targetAssemblyPointId;
            }
        }

        return result;
    }

    private static Dictionary<int, int> BuildActivityAssignments(
        SetAssemblyPointUnavailableWithReassignmentCommand request)
    {
        var result = new Dictionary<int, int>();

        foreach (var reassignment in request.MissionActivityReassignments)
        {
            AddActivityAssignment(result, reassignment.MissionActivityId, reassignment.TargetAssemblyPointId);
        }

        return result;
    }

    private static Dictionary<int, int> BuildStationedTeamAssignments(
        IReadOnlyList<TeamAssemblyPointReassignmentDto> reassignments)
    {
        var result = new Dictionary<int, int>();
        foreach (var reassignment in reassignments)
        {
            if (result.TryGetValue(reassignment.RescueTeamId, out var existingTarget)
                && existingTarget != reassignment.TargetAssemblyPointId)
            {
                throw new BadRequestException($"Đội cứu hộ #{reassignment.RescueTeamId} được gán tới nhiều điểm tập kết đích khác nhau.");
            }

            result[reassignment.RescueTeamId] = reassignment.TargetAssemblyPointId;
        }

        return result;
    }

    private static void AddActivityAssignment(Dictionary<int, int> assignments, int activityId, int targetAssemblyPointId)
    {
        if (assignments.TryGetValue(activityId, out var existingTarget) && existingTarget != targetAssemblyPointId)
        {
            throw new BadRequestException($"Mission activity #{activityId} được gán tới nhiều điểm tập kết đích khác nhau.");
        }

        assignments[activityId] = targetAssemblyPointId;
    }

    private static async Task ValidateTargetsAvailableAsync(
        IReadOnlyCollection<int> targetAssemblyPointIds,
        int sourceAssemblyPointId,
        IAssemblyPointRepository assemblyPointRepository,
        CancellationToken cancellationToken)
    {
        foreach (var targetId in targetAssemblyPointIds)
        {
            if (targetId == sourceAssemblyPointId)
            {
                throw new BadRequestException("Điểm tập kết đích không được trùng với điểm tập kết đang chuyển sang Không khả dụng.");
            }

            var target = await assemblyPointRepository.GetByIdAsync(targetId, cancellationToken)
                ?? throw new NotFoundException($"Không tìm thấy điểm tập kết đích id = {targetId}");

            if (target.Status != AssemblyPointStatus.Available)
            {
                throw new ConflictException($"Điểm tập kết đích {target.Name} hiện là {target.Status}, không còn Available.");
            }
        }
    }
    private static void EnsureNoUnknownIds<T>(IEnumerable<T> selected, IReadOnlySet<T> current, string subject)
        where T : notnull
    {
        var unknown = selected.Where(id => !current.Contains(id)).ToList();
        if (unknown.Count > 0)
        {
            throw new ConflictException($"Có {subject}: {string.Join(", ", unknown)}.");
        }
    }

    private static void EnsureAllIdsAssigned<T>(IReadOnlySet<T> current, IEnumerable<T> assigned, string subject)
        where T : notnull
    {
        var assignedSet = assigned.ToHashSet();
        var missing = current.Where(id => !assignedSet.Contains(id)).ToList();
        if (missing.Count > 0)
        {
            throw new BadRequestException($"Chưa chọn điểm tập kết đích cho {subject}: {string.Join(", ", missing)}.");
        }
    }

    private static async Task NotifyAffectedUsersAsync(
        IReadOnlyCollection<Guid> userIds,
        IFirebaseService firebaseService,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        foreach (var userId in userIds)
        {
            try
            {
                await firebaseService.SendNotificationToUserAsync(
                    userId,
                    "Điểm tập kết không còn khả dụng",
                    "Điểm tập kết hiện tại không còn khả dụng. Bạn đã được điều phối sang điểm tập kết mới.",
                    "assembly_point_unavailable_reassigned",
                    cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to send unavailable reassignment notification to user {UserId}", userId);
            }
        }
    }
}
