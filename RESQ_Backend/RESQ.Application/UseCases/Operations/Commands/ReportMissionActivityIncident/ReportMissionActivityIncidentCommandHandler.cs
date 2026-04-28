using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.Repositories.System;
using RESQ.Application.UseCases.Emergency.Commands.CreateSosRequest;
using RESQ.Application.UseCases.Operations.Commands.ReportTeamIncident;
using RESQ.Application.UseCases.Operations.Shared;
using RESQ.Domain.Entities.Emergency;
using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Application.UseCases.Operations.Commands.ReportMissionActivityIncident;

public class ReportMissionActivityIncidentCommandHandler(
    IMissionRepository missionRepository,
    IMissionActivityRepository missionActivityRepository,
    IMissionTeamRepository missionTeamRepository,
    ITeamIncidentRepository teamIncidentRepository,
    ISosRequestRepository sosRequestRepository,
    ISosClusterRepository sosClusterRepository,
    ISosPriorityRuleConfigRepository sosPriorityRuleConfigRepository,
    ISosRequestUpdateRepository sosRequestUpdateRepository,
    IDepotInventoryRepository depotInventoryRepository,
    IMediator mediator,
    IUnitOfWork unitOfWork,
    ILogger<ReportMissionActivityIncidentCommandHandler> logger
) : IRequestHandler<ReportMissionActivityIncidentCommand, ReportTeamIncidentResponse>
{
    public async Task<ReportTeamIncidentResponse> Handle(ReportMissionActivityIncidentCommand request, CancellationToken cancellationToken)
    {
        var normalized = IncidentV2NormalizationHelper.NormalizeActivityRequest(request.MissionId, request.MissionTeamId, request.Payload);

        var mission = await missionRepository.GetByIdAsync(request.MissionId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy mission #{request.MissionId}.");

        if (mission.Status != MissionStatus.OnGoing)
        {
            throw new BadRequestException($"Mission #{request.MissionId} không ở trạng thái OnGoing để báo activity incident.");
        }

        var missionTeam = await missionTeamRepository.GetByIdAsync(request.MissionTeamId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy liên kết đội-mission với ID: {request.MissionTeamId}");

        if (missionTeam.MissionId != request.MissionId)
        {
            throw new BadRequestException($"MissionTeamId={request.MissionTeamId} không thuộc mission #{request.MissionId}.");
        }

        EnsureReporterBelongsToTeam(missionTeam, request.ReportedBy);

        var missionActivities = (await missionActivityRepository.GetByMissionIdAsync(request.MissionId, cancellationToken))
            .OrderBy(activity => activity.Step ?? int.MaxValue)
            .ThenBy(activity => activity.Id)
            .ToList();

        var selectedActivities = missionActivities
            .Where(activity => normalized.ActivityIds.Contains(activity.Id))
            .ToList();

        if (selectedActivities.Count != normalized.ActivityIds.Count)
        {
            throw new NotFoundException("Có activity trong ActivityIds không tồn tại hoặc không thuộc mission hiện tại.");
        }

        foreach (var activity in selectedActivities)
        {
            if (activity.MissionTeamId != request.MissionTeamId)
            {
                throw new BadRequestException($"Activity #{activity.Id} không thuộc mission team #{request.MissionTeamId}.");
            }

            if (!MissionActivityIncidentFailureHelper.CanFailFromIncident(activity.Status))
            {
                throw new BadRequestException(
                    $"Activity #{activity.Id} đang ở trạng thái '{activity.Status}' nên không thể báo activity incident.");
            }
        }

        var primaryActivity = selectedActivities.FirstOrDefault(activity => activity.Id == normalized.PrimaryActivityId)
            ?? throw new NotFoundException($"Không tìm thấy primary activity #{normalized.PrimaryActivityId}.");

        var now = DateTime.UtcNow;
        var incident = new TeamIncidentModel
        {
            MissionTeamId = missionTeam.Id,
            MissionActivityId = primaryActivity.Id,
            IncidentScope = TeamIncidentScope.Activity,
            IncidentType = normalized.IncidentType ?? IncidentV2Constants.ActivityIncidentType,
            DecisionCode = normalized.DecisionCode,
            Description = normalized.Summary,
            Latitude = normalized.Latitude,
            Longitude = normalized.Longitude,
            DetailJson = normalized.DetailJson,
            PayloadVersion = IncidentV2Constants.PayloadVersion,
            NeedSupportSos = normalized.NeedSupportSos,
            NeedReassignActivity = normalized.NeedReassignActivity,
            Status = TeamIncidentStatus.Reported,
            ReportedBy = request.ReportedBy,
            ReportedAt = now,
            AffectedActivities = BuildAffectedActivities(selectedActivities, primaryActivity.Id)
        };

        var incidentId = 0;
        CreateSosRequestResponse? supportSos = null;
        IReadOnlyCollection<int> impactedSosRequestIds = [];
        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            incidentId = await teamIncidentRepository.CreateAsync(incident, cancellationToken);

            if (normalized.HasWorkloadImpact)
            {
                impactedSosRequestIds = (await SosRequestIncidentHelper.MarkSosRequestsAsIncidentAsync(
                    SosRequestIncidentHelper.ResolveLifecycleSosRequestIds(selectedActivities),
                    incidentId,
                    request.MissionId,
                    missionTeam,
                    primaryActivity,
                    normalized.Summary,
                    request.ReportedBy,
                    sosRequestRepository,
                    sosPriorityRuleConfigRepository,
                    sosRequestUpdateRepository,
                    logger,
                    cancellationToken)).ToList();
            }

            if (normalized.ShouldFailSelectedActivities)
            {
                await MissionActivityIncidentFailureHelper.FailActivitiesAsync(
                    selectedActivities,
                    request.ReportedBy,
                    missionActivityRepository,
                    missionTeamRepository,
                    sosRequestRepository,
                    sosClusterRepository,
                    sosRequestUpdateRepository,
                    teamIncidentRepository,
                    depotInventoryRepository,
                    unitOfWork,
                    logger,
                    allowAutoChain: false,
                    allowReturnSuppliesCreation: true,
                    allowSosLifecycleSync: false,
                    cancellationToken);

                var earliestAffected = selectedActivities
                    .OrderBy(activity => activity.Step ?? int.MaxValue)
                    .ThenBy(activity => activity.Id)
                    .First();

                await MissionActivityAutoStartHelper.AutoStartNextActivityForSameTeamAsync(
                    earliestAffected,
                    request.ReportedBy,
                    missionActivityRepository,
                    missionTeamRepository,
                    logger,
                    cancellationToken);

                foreach (var affectedActivity in incident.AffectedActivities)
                {
                    affectedActivity.Status = MissionActivityStatus.Failed;
                }
            }

            if (normalized.NeedReassignActivity)
            {
                await missionActivityRepository.ResetAssignmentsToPlannedAsync(
                    selectedActivities.Select(activity => activity.Id),
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
                primaryActivity,
                request.ReportedBy,
                normalized.IncidentType ?? IncidentV2Constants.ActivityIncidentType,
                normalized.DecisionCode,
                normalized.Summary,
                normalized.Latitude,
                normalized.Longitude,
                normalized.SosContext,
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
                    primaryActivity.Id,
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

            await unitOfWork.SaveAsync();
        });

        return new ReportTeamIncidentResponse
        {
            IncidentId = incidentId,
            MissionId = request.MissionId,
            MissionTeamId = missionTeam.Id,
            MissionActivityId = primaryActivity.Id,
            IncidentScope = TeamIncidentScope.Activity.ToString(),
            IncidentType = normalized.IncidentType ?? IncidentV2Constants.ActivityIncidentType,
            DecisionCode = normalized.DecisionCode,
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
        int primaryActivityId)
    {
        return activities
            .OrderBy(activity => activity.Step ?? int.MaxValue)
            .ThenBy(activity => activity.Id)
            .Select((activity, index) => new TeamIncidentAffectedActivityModel
            {
                MissionActivityId = activity.Id,
                OrderIndex = index,
                IsPrimary = activity.Id == primaryActivityId,
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
        int missionActivityId,
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
                IncidentScope = TeamIncidentScope.Activity.ToString(),
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
