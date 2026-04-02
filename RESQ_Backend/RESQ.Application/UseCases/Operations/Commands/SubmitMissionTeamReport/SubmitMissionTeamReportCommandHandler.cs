using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Repositories.Identity;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.UseCases.Operations.Queries.GetMissionTeamReport;
using RESQ.Application.UseCases.Operations.Shared;
using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Enum.Emergency;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Application.UseCases.Operations.Commands.SubmitMissionTeamReport;

public class SubmitMissionTeamReportCommandHandler(
    IMissionRepository missionRepository,
    IMissionActivityRepository missionActivityRepository,
    IMissionTeamRepository missionTeamRepository,
    IMissionTeamReportRepository missionTeamReportRepository,
    IRescuerScoreRepository rescuerScoreRepository,
    ISosRequestRepository sosRequestRepository,
    IUnitOfWork unitOfWork,
    ILogger<SubmitMissionTeamReportCommandHandler> logger)
    : IRequestHandler<SubmitMissionTeamReportCommand, MissionTeamReportResponse>
{
    public async Task<MissionTeamReportResponse> Handle(SubmitMissionTeamReportCommand request, CancellationToken cancellationToken)
    {
        var mission = await missionRepository.GetByIdAsync(request.MissionId, cancellationToken)
            ?? throw new NotFoundException($"KhÃ´ng tÃ¬m tháº¥y mission vá»›i ID: {request.MissionId}");

        var missionTeam = await missionTeamRepository.GetByIdAsync(request.MissionTeamId, cancellationToken)
            ?? throw new NotFoundException($"KhÃ´ng tÃ¬m tháº¥y liÃªn káº¿t Ä‘á»™i-mission vá»›i ID: {request.MissionTeamId}");

        if (missionTeam.MissionId != request.MissionId)
            throw new BadRequestException("Mission team khÃ´ng thuá»™c mission Ä‘Æ°á»£c yÃªu cáº§u.");

        var leader = missionTeam.RescueTeamMembers.FirstOrDefault(x => x.UserId == request.SubmittedBy && x.IsLeader);
        if (leader is null)
            throw new ForbiddenException("Chá»‰ Ä‘á»™i trÆ°á»Ÿng má»›i cÃ³ quyá»n ná»™p bÃ¡o cÃ¡o cuá»‘i cÃ¹ng.");

        if (string.Equals(missionTeam.Status, MissionTeamExecutionStatus.Cancelled.ToString(), StringComparison.OrdinalIgnoreCase))
            throw new BadRequestException("Äá»™i Ä‘Ã£ bá»‹ há»§y phÃ¢n cÃ´ng, khÃ´ng thá»ƒ ná»™p bÃ¡o cÃ¡o.");

        if (!string.Equals(missionTeam.Status, MissionTeamExecutionStatus.CompletedWaitingReport.ToString(), StringComparison.OrdinalIgnoreCase))
            throw new BadRequestException("Äá»™i pháº£i hoÃ n táº¥t thá»±c thi trÆ°á»›c khi ná»™p bÃ¡o cÃ¡o cuá»‘i cÃ¹ng.");

        if (string.Equals(missionTeam.ReportStatus, MissionTeamReportStatus.Submitted.ToString(), StringComparison.OrdinalIgnoreCase)
            || string.Equals(missionTeam.Status, MissionTeamExecutionStatus.Reported.ToString(), StringComparison.OrdinalIgnoreCase))
            throw new ConflictException("BÃ¡o cÃ¡o cuá»‘i cÃ¹ng Ä‘Ã£ Ä‘Æ°á»£c ná»™p trÆ°á»›c Ä‘Ã³.");

        var assignedActivities = mission.Activities
            .Where(x => x.MissionTeamId == request.MissionTeamId)
            .ToDictionary(x => x.Id);

        if (assignedActivities.Count == 0)
            throw new BadRequestException("Äá»™i nÃ y chÆ°a Ä‘Æ°á»£c giao activity nÃ o Ä‘á»ƒ bÃ¡o cÃ¡o.");

        var invalidActivityId = request.Activities
            .Select(x => x.MissionActivityId)
            .FirstOrDefault(id => !assignedActivities.ContainsKey(id));

        if (invalidActivityId > 0)
            throw new BadRequestException($"Activity #{invalidActivityId} khÃ´ng thuá»™c mission team nÃ y.");

        var memberEvaluations = request.MemberEvaluations
            .Select(x => new MissionTeamMemberEvaluationModel
            {
                RescuerId = x.RescuerId,
                ResponseTimeScore = x.ResponseTimeScore,
                RescueEffectivenessScore = x.RescueEffectivenessScore,
                DecisionHandlingScore = x.DecisionHandlingScore,
                SafetyMedicalSkillScore = x.SafetyMedicalSkillScore,
                TeamworkCommunicationScore = x.TeamworkCommunicationScore
            })
            .ToList();

        MissionTeamMemberEvaluationHelper.ValidateSubmit(memberEvaluations, missionTeam);

        var activityStatusUpdates = new List<(int ActivityId, MissionActivityStatus Status)>();
        foreach (var item in request.Activities)
        {
            if (string.IsNullOrWhiteSpace(item.ExecutionStatus))
            {
                continue;
            }

            if (!TryMapExecutionStatus(item.ExecutionStatus, out var mappedStatus))
            {
                throw new BadRequestException(
                    $"ExecutionStatus '{item.ExecutionStatus}' cá»§a activity #{item.MissionActivityId} khÃ´ng há»£p lá»‡.");
            }

            activityStatusUpdates.Add((item.MissionActivityId, mappedStatus));
        }

        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await missionTeamReportRepository.UpsertDraftAsync(new MissionTeamReportModel
            {
                MissionTeamId = request.MissionTeamId,
                ReportStatus = MissionTeamReportStatus.Draft,
                TeamSummary = request.TeamSummary,
                TeamNote = request.TeamNote,
                IssuesJson = request.IssuesJson,
                ResultJson = request.ResultJson,
                EvidenceJson = request.EvidenceJson,
                ActivityReports = request.Activities.Select(x =>
                {
                    var activity = assignedActivities[x.MissionActivityId];
                    return new MissionActivityReportModel
                    {
                        MissionActivityId = x.MissionActivityId,
                        ActivityCode = activity.ActivityCode,
                        ActivityType = activity.ActivityType,
                        ExecutionStatus = x.ExecutionStatus,
                        Summary = x.Summary,
                        IssuesJson = x.IssuesJson,
                        ResultJson = x.ResultJson,
                        EvidenceJson = x.EvidenceJson
                    };
                }).ToList(),
                MemberEvaluations = memberEvaluations
            }, cancellationToken);

            foreach (var statusUpdate in activityStatusUpdates)
            {
                var effectiveStatus = statusUpdate.Status;

                // Intercept: RETURN_SUPPLIES cannot go directly to Succeed
                if (assignedActivities.TryGetValue(statusUpdate.ActivityId, out var activityForUpdate)
                    && string.Equals(activityForUpdate.ActivityType, "RETURN_SUPPLIES", StringComparison.OrdinalIgnoreCase)
                    && effectiveStatus == MissionActivityStatus.Succeed
                    && activityForUpdate.Status == MissionActivityStatus.OnGoing)
                {
                    effectiveStatus = MissionActivityStatus.PendingConfirmation;
                    logger.LogInformation(
                        "RETURN_SUPPLIES ActivityId={activityId}: intercepted Succeed → PendingConfirmation in report",
                        statusUpdate.ActivityId);
                }

                await missionActivityRepository.UpdateStatusAsync(
                    statusUpdate.ActivityId,
                    effectiveStatus,
                    request.SubmittedBy,
                    cancellationToken);

                assignedActivities[statusUpdate.ActivityId].Status = effectiveStatus;
            }

            // Auto-create RETURN_SUPPLIES for each failed DELIVER_SUPPLIES with items
            foreach (var statusUpdate in activityStatusUpdates)
            {
                if (statusUpdate.Status != MissionActivityStatus.Failed) continue;
                if (!assignedActivities.TryGetValue(statusUpdate.ActivityId, out var failedActivity)) continue;
                if (!string.Equals(failedActivity.ActivityType, "DELIVER_SUPPLIES", StringComparison.OrdinalIgnoreCase)) continue;
                if (string.IsNullOrWhiteSpace(failedActivity.Items) || !failedActivity.DepotId.HasValue) continue;

                try
                {
                    var maxStep = mission.Activities.Any() ? mission.Activities.Max(a => a.Step ?? 0) : 0;
                    var returnActivity = new MissionActivityModel
                    {
                        MissionId = request.MissionId,
                        Step = maxStep + 1,
                        ActivityCode = $"RET-{failedActivity.ActivityCode}",
                        ActivityType = "RETURN_SUPPLIES",
                        Description = $"Trả vật tư về kho {failedActivity.DepotName} do giao hàng thất bại (Activity #{failedActivity.Id})",
                        Priority = failedActivity.Priority,
                        EstimatedTime = failedActivity.EstimatedTime,
                        SosRequestId = failedActivity.SosRequestId,
                        DepotId = failedActivity.DepotId,
                        DepotName = failedActivity.DepotName,
                        DepotAddress = failedActivity.DepotAddress,
                        Items = failedActivity.Items,
                        Status = MissionActivityStatus.Planned
                    };

                    var returnActivityId = await missionActivityRepository.AddAsync(returnActivity, cancellationToken);
                    returnActivity.Id = returnActivityId;

                    if (failedActivity.MissionTeamId.HasValue)
                    {
                        await missionActivityRepository.AssignTeamAsync(returnActivityId, failedActivity.MissionTeamId.Value, cancellationToken);
                        returnActivity.MissionTeamId = failedActivity.MissionTeamId;
                    }

                    // Add to mission.Activities so completion check sees it as unsettled
                    mission.Activities.Add(returnActivity);

                    logger.LogInformation(
                        "Auto-created RETURN_SUPPLIES ActivityId={returnActivityId} for failed DELIVER_SUPPLIES ActivityId={failedActivityId} in report",
                        returnActivityId, failedActivity.Id);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex,
                        "Failed to auto-create RETURN_SUPPLIES for DELIVER_SUPPLIES ActivityId={activityId} in report",
                        failedActivity.Id);
                }
            }

            await missionTeamReportRepository.SubmitAsync(request.MissionTeamId, request.SubmittedBy, cancellationToken);
            await rescuerScoreRepository.RefreshAsync(memberEvaluations.Select(x => x.RescuerId), cancellationToken);
            await missionTeamRepository.UpdateStatusAsync(request.MissionTeamId, MissionTeamExecutionStatus.Reported.ToString(), cancellationToken);

            var refreshedTeams = (await missionTeamRepository.GetByMissionIdAsync(request.MissionId, cancellationToken))
                .Where(x => !string.Equals(x.Status, MissionTeamExecutionStatus.Cancelled.ToString(), StringComparison.OrdinalIgnoreCase))
                .ToList();

            var requiredTeamIds = mission.Activities
                .Where(x => x.MissionTeamId.HasValue && x.Status != MissionActivityStatus.Cancelled)
                .Select(x => x.MissionTeamId!.Value)
                .Distinct()
                .ToList();

            var allRequiredTeamsReported = requiredTeamIds.Count > 0
                && requiredTeamIds.All(teamId => refreshedTeams.Any(x => x.Id == teamId
                    && string.Equals(x.Status, MissionTeamExecutionStatus.Reported.ToString(), StringComparison.OrdinalIgnoreCase)));

            var allActivitiesSettled = mission.Activities.Count > 0
                && mission.Activities.All(IsActivitySettledForMissionCompletion);

            if (allActivitiesSettled && allRequiredTeamsReported)
            {
                await missionRepository.UpdateStatusAsync(request.MissionId, MissionStatus.Completed, isCompleted: true, cancellationToken);
                if (mission.ClusterId.HasValue)
                {
                    await sosRequestRepository.UpdateStatusByClusterIdAsync(mission.ClusterId.Value, SosRequestStatus.Resolved, cancellationToken);
                }
            }
        });

        var refreshedMissionTeam = await missionTeamRepository.GetByIdAsync(request.MissionTeamId, cancellationToken)
            ?? throw new NotFoundException($"KhÃ´ng tÃ¬m tháº¥y liÃªn káº¿t Ä‘á»™i-mission vá»›i ID: {request.MissionTeamId}");
        var report = await missionTeamReportRepository.GetByMissionTeamIdAsync(request.MissionTeamId, cancellationToken);

        return MissionTeamReportResponseFactory.Create(request.MissionId, refreshedMissionTeam, report, assignedActivities.Values, request.SubmittedBy);
    }

    private static bool IsActivitySettledForMissionCompletion(MissionActivityModel activity)
    {
        if (activity.Status == MissionActivityStatus.Cancelled)
        {
            return true;
        }

        return activity.MissionTeamId.HasValue
            && activity.Status is MissionActivityStatus.Succeed or MissionActivityStatus.Failed;
    }

    private static bool TryMapExecutionStatus(string executionStatus, out MissionActivityStatus status)
    {
        switch (executionStatus.Trim().ToLowerInvariant())
        {
            case "planned":
            case "pending":
                status = MissionActivityStatus.Planned;
                return true;
            case "ongoing":
            case "on-going":
            case "on_going":
            case "inprogress":
            case "in_progress":
            case "in-progress":
                status = MissionActivityStatus.OnGoing;
                return true;
            case "completed":
            case "complete":
            case "succeed":
            case "succeeded":
            case "success":
                status = MissionActivityStatus.Succeed;
                return true;
            case "failed":
            case "fail":
                status = MissionActivityStatus.Failed;
                return true;
            case "cancelled":
            case "canceled":
            case "cancel":
                status = MissionActivityStatus.Cancelled;
                return true;
            case "pending_confirmation":
            case "pendingconfirmation":
            case "pending-confirmation":
                status = MissionActivityStatus.PendingConfirmation;
                return true;
            default:
                return Enum.TryParse(executionStatus, ignoreCase: true, out status);
        }
    }
}
