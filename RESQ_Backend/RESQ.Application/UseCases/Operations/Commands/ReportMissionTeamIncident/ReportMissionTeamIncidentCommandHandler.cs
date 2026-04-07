using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.Repositories.Personnel;
using RESQ.Application.Repositories.System;
using RESQ.Application.UseCases.Emergency.Commands.CreateSosRequest;
using RESQ.Application.UseCases.Operations.Commands.ReportTeamIncident;
using RESQ.Application.UseCases.Operations.Shared;
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
    IDepotInventoryRepository depotInventoryRepository,
    IMediator mediator,
    IUnitOfWork unitOfWork,
    ILogger<ReportMissionTeamIncidentCommandHandler> logger
) : IRequestHandler<ReportMissionTeamIncidentCommand, ReportTeamIncidentResponse>
{
    public async Task<ReportTeamIncidentResponse> Handle(ReportMissionTeamIncidentCommand request, CancellationToken cancellationToken)
    {
        ValidateRequest(request.Description, request.Latitude, request.Longitude);

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

        var unfinishedActivities = (await missionActivityRepository.GetByMissionIdAsync(request.MissionId, cancellationToken))
            .Where(activity => activity.MissionTeamId == request.MissionTeamId
                && MissionActivityIncidentFailureHelper.CanFailFromIncident(activity.Status))
            .OrderBy(activity => activity.Step ?? int.MaxValue)
            .ThenBy(activity => activity.Id)
            .ToList();

        if (unfinishedActivities.Count == 0)
        {
            throw new BadRequestException(
                $"MissionTeamId={request.MissionTeamId} không còn activity chưa hoàn tất để xử lý mission incident.");
        }

        var now = DateTime.UtcNow;
        var incident = new TeamIncidentModel
        {
            MissionTeamId = request.MissionTeamId,
            IncidentScope = TeamIncidentScope.Mission,
            Description = request.Description.Trim(),
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            Status = TeamIncidentStatus.Reported,
            ReportedBy = request.ReportedBy,
            ReportedAt = now
        };

        var rescueTeam = await rescueTeamRepository.GetByIdAsync(missionTeam.RescuerTeamId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy đội cứu hộ với ID: {missionTeam.RescuerTeamId}");

        rescueTeam.ReportIncident();

        var incidentId = 0;
        CreateSosRequestResponse? assistanceSos = null;
        IReadOnlyCollection<int> impactedSosRequestIds = [];
        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            incidentId = await teamIncidentRepository.CreateAsync(incident, cancellationToken);
            await rescueTeamRepository.UpdateAsync(rescueTeam, cancellationToken);

            await MissionActivityIncidentFailureHelper.FailActivitiesAsync(
                unfinishedActivities,
                request.ReportedBy,
                missionActivityRepository,
                missionTeamRepository,
                sosRequestRepository,
                depotInventoryRepository,
                unitOfWork,
                logger,
                allowAutoChain: false,
                allowReturnSuppliesCreation: false,
                allowSosLifecycleSync: false,
                cancellationToken);

            impactedSosRequestIds = (await SosRequestIncidentHelper.MarkSosRequestsAsIncidentAsync(
                SosRequestIncidentHelper.ResolveLifecycleSosRequestIds(unfinishedActivities),
                incidentId,
                request.MissionId,
                missionTeam,
                activity: null,
                request.Description,
                request.ReportedBy,
                sosRequestRepository,
                sosPriorityRuleConfigRepository,
                sosRequestUpdateRepository,
                logger,
                cancellationToken)).ToList();

            assistanceSos = await TeamIncidentAssistanceSosHelper.CreateAssistanceSosAsync(
                request.MissionId,
                missionTeam,
                activity: null,
                request.ReportedBy,
                request.Description,
                request.Latitude,
                request.Longitude,
                request.NeedsRescueAssistance,
                request.AssistanceSos,
                mediator,
                logger,
                cancellationToken);

            await missionTeamRepository.UpdateStatusAsync(
                missionTeam.Id,
                MissionTeamExecutionStatus.CompletedWaitingReport.ToString(),
                cancellationToken);

            await missionRepository.UpdateStatusAsync(request.MissionId, MissionStatus.Incompleted, isCompleted: true, cancellationToken);
            await unitOfWork.SaveAsync();
        });

        return new ReportTeamIncidentResponse
        {
            IncidentId = incidentId,
            MissionId = request.MissionId,
            MissionTeamId = request.MissionTeamId,
            IncidentScope = TeamIncidentScope.Mission.ToString(),
            Status = TeamIncidentStatus.Reported.ToString(),
            IncidentSosRequestIds = impactedSosRequestIds.ToList(),
            AssistanceSosRequestId = assistanceSos?.Id,
            AssistanceSosStatus = assistanceSos?.Status,
            AssistanceSosPriorityLevel = assistanceSos?.PriorityLevel,
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

    private static void ValidateRequest(string description, double? latitude, double? longitude)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new BadRequestException("Nội dung incident không được để trống.");
        }

        if (latitude.HasValue != longitude.HasValue)
        {
            throw new BadRequestException("Latitude và Longitude phải cùng có giá trị hoặc cùng để trống.");
        }
    }
}