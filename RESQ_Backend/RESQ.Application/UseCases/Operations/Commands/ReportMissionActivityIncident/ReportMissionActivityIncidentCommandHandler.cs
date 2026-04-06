using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.Repositories.Personnel;
using RESQ.Application.UseCases.Operations.Commands.ReportTeamIncident;
using RESQ.Application.UseCases.Operations.Shared;
using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Application.UseCases.Operations.Commands.ReportMissionActivityIncident;

public class ReportMissionActivityIncidentCommandHandler(
    IMissionRepository missionRepository,
    IMissionActivityRepository missionActivityRepository,
    IMissionTeamRepository missionTeamRepository,
    ITeamIncidentRepository teamIncidentRepository,
    IRescueTeamRepository rescueTeamRepository,
    IDepotInventoryRepository depotInventoryRepository,
    IUnitOfWork unitOfWork,
    ILogger<ReportMissionActivityIncidentCommandHandler> logger
) : IRequestHandler<ReportMissionActivityIncidentCommand, ReportTeamIncidentResponse>
{
    public async Task<ReportTeamIncidentResponse> Handle(ReportMissionActivityIncidentCommand request, CancellationToken cancellationToken)
    {
        ValidateRequest(request.Description, request.Latitude, request.Longitude);

        var mission = await missionRepository.GetByIdAsync(request.MissionId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy mission #{request.MissionId}.");

        if (mission.Status != MissionStatus.OnGoing)
        {
            throw new BadRequestException($"Mission #{request.MissionId} không ở trạng thái OnGoing để báo activity incident.");
        }

        var activity = await missionActivityRepository.GetByIdAsync(request.ActivityId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy activity với ID: {request.ActivityId}");

        if (activity.MissionId != request.MissionId)
        {
            throw new BadRequestException($"Activity #{request.ActivityId} không thuộc mission #{request.MissionId}.");
        }

        if (!activity.MissionTeamId.HasValue)
        {
            throw new BadRequestException($"Activity #{request.ActivityId} chưa được gán cho mission team nào.");
        }

        if (!MissionActivityIncidentFailureHelper.CanFailFromIncident(activity.Status))
        {
            throw new BadRequestException(
                $"Activity #{request.ActivityId} đang ở trạng thái '{activity.Status}' nên không thể báo activity incident.");
        }

        var missionTeam = await missionTeamRepository.GetByIdAsync(activity.MissionTeamId.Value, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy liên kết đội-mission với ID: {activity.MissionTeamId.Value}");

        EnsureReporterBelongsToTeam(missionTeam, request.ReportedBy);

        var now = DateTime.UtcNow;
        var incident = new TeamIncidentModel
        {
            MissionTeamId = missionTeam.Id,
            MissionActivityId = activity.Id,
            IncidentScope = TeamIncidentScope.Activity,
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
        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            incidentId = await teamIncidentRepository.CreateAsync(incident, cancellationToken);
            await rescueTeamRepository.UpdateAsync(rescueTeam, cancellationToken);

            await MissionActivityIncidentFailureHelper.FailSingleActivityAsync(
                activity,
                request.ReportedBy,
                missionActivityRepository,
                missionTeamRepository,
                depotInventoryRepository,
                unitOfWork,
                logger,
                allowAutoChain: true,
                allowReturnSuppliesCreation: true,
                cancellationToken);

            await unitOfWork.SaveAsync();
        });

        return new ReportTeamIncidentResponse
        {
            IncidentId = incidentId,
            MissionId = request.MissionId,
            MissionTeamId = missionTeam.Id,
            MissionActivityId = request.ActivityId,
            IncidentScope = TeamIncidentScope.Activity.ToString(),
            Status = TeamIncidentStatus.Reported.ToString(),
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