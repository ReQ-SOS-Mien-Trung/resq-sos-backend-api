using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.Repositories.Personnel;
using RESQ.Application.Repositories.System;
using RESQ.Application.UseCases.Emergency.Commands.CreateSosRequest;
using RESQ.Application.UseCases.Operations.Commands.ReportTeamIncident;
using RESQ.Application.UseCases.Operations.Shared;
using RESQ.Domain.Entities.Emergency;
using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Application.UseCases.Operations.Commands.ReportMissionTeamIncident;

public class ReportMissionTeamIncidentCommandHandler(
    IMissionRepository missionRepository,
    IMissionTeamRepository missionTeamRepository,
    ITeamIncidentRepository teamIncidentRepository,
    IMissionActivityRepository missionActivityRepository,
    IRescueTeamRepository rescueTeamRepository,
    ISosRequestRepository sosRequestRepository,
    ISosPriorityRuleConfigRepository sosPriorityRuleConfigRepository,
    ISosRequestUpdateRepository sosRequestUpdateRepository,
    IMediator mediator,
    IUnitOfWork unitOfWork,
    ILogger<ReportMissionTeamIncidentCommandHandler> logger
) : IRequestHandler<ReportMissionTeamIncidentCommand, ReportTeamIncidentResponse>
{
    public async Task<ReportTeamIncidentResponse> Handle(ReportMissionTeamIncidentCommand request, CancellationToken cancellationToken)
    {
        var normalized = IncidentV2NormalizationHelper.NormalizeMissionRequest(request.Payload);

        var mission = await missionRepository.GetByIdAsync(request.MissionId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy mission #{request.MissionId}.");

        if (mission.Status != MissionStatus.OnGoing)
        {
            throw new BadRequestException($"Mission #{request.MissionId} không ở trạng thái OnGoing để báo mission incident.");
        }

        var missionTeam = await missionTeamRepository.GetByIdAsync(request.MissionTeamId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy liên kết đội-mission với ID: {request.MissionTeamId}");

        if (missionTeam.MissionId != request.MissionId)
        {
            throw new BadRequestException($"MissionTeamId={request.MissionTeamId} không thuộc mission #{request.MissionId}.");
        }

        EnsureReporterBelongsToTeam(missionTeam, request.ReportedBy);

        var missionActivities = (await missionActivityRepository.GetByMissionIdAsync(request.MissionId, cancellationToken))
            .Where(activity => activity.MissionTeamId == request.MissionTeamId)
            .OrderBy(activity => activity.Step ?? int.MaxValue)
            .ThenBy(activity => activity.Id)
            .ToList();

        var unfinishedActivities = missionActivities
            .Where(activity => activity.MissionTeamId == request.MissionTeamId
                && MissionActivityIncidentFailureHelper.CanFailFromIncident(activity.Status))
            .ToList();

        if (normalized.MissionDecision != IncidentV2Constants.MissionDecisionCodes.ContinueMission
            && unfinishedActivities.Count == 0)
        {
            throw new BadRequestException(
                $"MissionTeamId={request.MissionTeamId} không còn activity chưa hoàn tất để xử lý mission incident.");
        }

        var impactedActivities = normalized.MissionDecision == IncidentV2Constants.MissionDecisionCodes.ContinueMission
            ? []
            : unfinishedActivities;
        var primaryActivityId = impactedActivities.FirstOrDefault()?.Id;

        var now = DateTime.UtcNow;
        var incident = new TeamIncidentModel
        {
            MissionTeamId = request.MissionTeamId,
            MissionActivityId = primaryActivityId,
            IncidentScope = TeamIncidentScope.Mission,
            IncidentType = IncidentV2Constants.MissionIncidentType,
            DecisionCode = normalized.MissionDecision,
            Description = normalized.Summary,
            Latitude = normalized.Latitude,
            Longitude = normalized.Longitude,
            DetailJson = normalized.DetailJson,
            PayloadVersion = IncidentV2Constants.PayloadVersion,
            NeedSupportSos = normalized.NeedSupportSos,
            NeedReassignActivity = false,
            Status = TeamIncidentStatus.Reported,
            ReportedBy = request.ReportedBy,
            ReportedAt = now,
            AffectedActivities = BuildAffectedActivities(impactedActivities, primaryActivityId)
        };

        var requiresRescueTeamStateChange = normalized.MissionDecision == IncidentV2Constants.MissionDecisionCodes.RescueWholeTeamImmediately;
        var rescueTeam = requiresRescueTeamStateChange
            ? await rescueTeamRepository.GetByIdAsync(missionTeam.RescuerTeamId, cancellationToken)
                ?? throw new NotFoundException($"Không tìm thấy đội cứu hộ với ID: {missionTeam.RescuerTeamId}")
            : null;

        var incidentId = 0;
        CreateSosRequestResponse? supportSos = null;
        IReadOnlyCollection<int> impactedSosRequestIds = [];
        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            incidentId = await teamIncidentRepository.CreateAsync(incident, cancellationToken);

            if (normalized.MissionDecision != IncidentV2Constants.MissionDecisionCodes.ContinueMission)
            {
                impactedSosRequestIds = (await SosRequestIncidentHelper.MarkSosRequestsAsIncidentAsync(
                    SosRequestIncidentHelper.ResolveLifecycleSosRequestIds(impactedActivities),
                    incidentId,
                    request.MissionId,
                    missionTeam,
                    activity: null,
                    normalized.Summary,
                    request.ReportedBy,
                    sosRequestRepository,
                    sosPriorityRuleConfigRepository,
                    sosRequestUpdateRepository,
                    logger,
                    cancellationToken)).ToList();

                await missionTeamRepository.UpdateStatusAsync(
                    missionTeam.Id,
                    MissionTeamExecutionStatus.Reported.ToString(),
                    cancellationToken);
            }

            if (normalized.MissionDecision == IncidentV2Constants.MissionDecisionCodes.HandoverMission && impactedActivities.Count > 0)
            {
                await missionActivityRepository.ResetAssignmentsToPlannedAsync(
                    impactedActivities.Select(activity => activity.Id),
                    request.ReportedBy,
                    cancellationToken);

                foreach (var affectedActivity in incident.AffectedActivities)
                {
                    affectedActivity.Status = MissionActivityStatus.Planned;
                }
            }

            supportSos = await TeamIncidentAssistanceSosHelper.CreateSupportSosAsync(
                request.MissionId,
                missionTeam,
                activity: impactedActivities.FirstOrDefault(),
                request.ReportedBy,
                IncidentV2Constants.MissionIncidentType,
                normalized.MissionDecision,
                normalized.Summary,
                normalized.Latitude,
                normalized.Longitude,
                normalized.NeedSupportSos,
                needReassignActivity: false,
                normalized.SupportRequest,
                mediator,
                logger,
                cancellationToken);

            if (supportSos is not null)
            {
                await LinkSupportSosAsync(
                    incidentId,
                    supportSos.Id,
                    request.MissionId,
                    missionTeam,
                    primaryActivityId,
                    normalized.Summary,
                    request.ReportedBy,
                    sosRequestUpdateRepository,
                    teamIncidentRepository,
                    cancellationToken);

                impactedSosRequestIds = impactedSosRequestIds
                    .Concat([supportSos.Id])
                    .Distinct()
                    .ToList();
            }

            if (rescueTeam is not null)
            {
                rescueTeam.ReportIncident();
                await rescueTeamRepository.UpdateAsync(rescueTeam, cancellationToken);
            }

            await unitOfWork.SaveAsync();
        });

        return new ReportTeamIncidentResponse
        {
            IncidentId = incidentId,
            MissionId = request.MissionId,
            MissionTeamId = request.MissionTeamId,
            MissionActivityId = primaryActivityId,
            IncidentScope = TeamIncidentScope.Mission.ToString(),
            IncidentType = IncidentV2Constants.MissionIncidentType,
            DecisionCode = normalized.MissionDecision,
            Status = TeamIncidentStatus.Reported.ToString(),
            IncidentSosRequestIds = impactedSosRequestIds.ToList(),
            HasSupportRequest = normalized.NeedSupportSos || supportSos is not null,
            SupportSosRequestId = supportSos?.Id,
            AssistanceSosRequestId = supportSos?.Id,
            AssistanceSosStatus = supportSos?.Status,
            AssistanceSosPriorityLevel = supportSos?.PriorityLevel,
            AffectedActivities = incident.AffectedActivities.Select(MapAffectedActivity).ToList(),
            Detail = ParseDetail(normalized.DetailJson),
            ReportedAt = now
        };
    }

    private static void EnsureReporterBelongsToTeam(MissionTeamModel missionTeam, Guid reportedBy)
    {
        if (!missionTeam.RescueTeamMembers.Any(member => member.UserId == reportedBy))
        {
            throw new ForbiddenException("Bạn không phải thành viên của đội cứu hộ này.");
        }
    }

    private static List<TeamIncidentAffectedActivityModel> BuildAffectedActivities(
        IEnumerable<MissionActivityModel> activities,
        int? primaryActivityId)
    {
        return activities
            .OrderBy(activity => activity.Step ?? int.MaxValue)
            .ThenBy(activity => activity.Id)
            .Select((activity, index) => new TeamIncidentAffectedActivityModel
            {
                MissionActivityId = activity.Id,
                OrderIndex = index,
                IsPrimary = primaryActivityId.HasValue && activity.Id == primaryActivityId.Value,
                Step = activity.Step,
                ActivityType = activity.ActivityType,
                Status = activity.Status
            })
            .ToList();
    }

    private static IncidentAffectedActivityDto MapAffectedActivity(TeamIncidentAffectedActivityModel activity) => new()
    {
        MissionActivityId = activity.MissionActivityId,
        OrderIndex = activity.OrderIndex,
        IsPrimary = activity.IsPrimary,
        Step = activity.Step,
        ActivityType = activity.ActivityType,
        Status = activity.Status?.ToString()
    };

    private static JsonElement? ParseDetail(string detailJson)
    {
        using var document = JsonDocument.Parse(detailJson);
        return document.RootElement.Clone();
    }

    private static async Task LinkSupportSosAsync(
        int incidentId,
        int supportSosRequestId,
        int missionId,
        MissionTeamModel missionTeam,
        int? missionActivityId,
        string note,
        Guid reportedBy,
        ISosRequestUpdateRepository sosRequestUpdateRepository,
        ITeamIncidentRepository teamIncidentRepository,
        CancellationToken cancellationToken)
    {
        await teamIncidentRepository.UpdateSupportSosRequestIdAsync(incidentId, supportSosRequestId, cancellationToken);
        await sosRequestUpdateRepository.AddIncidentRangeAsync(
        [
            new SosRequestIncidentUpdateModel
            {
                SosRequestId = supportSosRequestId,
                TeamIncidentId = incidentId,
                MissionId = missionId,
                MissionTeamId = missionTeam.Id,
                MissionActivityId = missionActivityId,
                IncidentScope = TeamIncidentScope.Mission.ToString(),
                Note = note,
                ReportedById = reportedBy,
                CreatedAt = DateTime.UtcNow,
                TeamName = missionTeam.TeamName,
                ActivityType = null
            }
        ],
        cancellationToken);
    }
}