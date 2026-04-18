using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.Repositories.Personnel;
using RESQ.Application.UseCases.Operations.Commands.UpdateActivityStatus;
using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Application.UseCases.Operations.Commands.ReportTeamIncident;

public class ReportTeamIncidentCommandHandler(
    IMissionTeamRepository missionTeamRepository,
    ITeamIncidentRepository teamIncidentRepository,
    IMissionActivityRepository missionActivityRepository,
    IRescueTeamRepository rescueTeamRepository,
    IMediator mediator,
    IUnitOfWork unitOfWork
) : IRequestHandler<ReportTeamIncidentCommand, ReportTeamIncidentResponse>
{
    public async Task<ReportTeamIncidentResponse> Handle(ReportTeamIncidentCommand request, CancellationToken cancellationToken)
    {
        var missionTeam = await missionTeamRepository.GetByIdAsync(request.MissionTeamId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy liên kết đội-mission với ID: {request.MissionTeamId}");

        // Verify the reporter belongs to this rescue team
        if (!missionTeam.RescueTeamMembers.Any(m => m.UserId == request.ReportedBy))
            throw new ForbiddenException("Bạn không phải thành viên của đội cứu hộ này.");

        var now = DateTime.UtcNow;
        var incident = new TeamIncidentModel
        {
            MissionTeamId = request.MissionTeamId,
            MissionActivityId = request.MissionActivityId,
            IncidentScope = request.MissionActivityId.HasValue ? TeamIncidentScope.Activity : TeamIncidentScope.Mission,
            Description = request.Description,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            Status = TeamIncidentStatus.Reported,
            ReportedBy = request.ReportedBy,
            ReportedAt = now
        };

        var incidentId = await teamIncidentRepository.CreateAsync(incident, cancellationToken);

        var rescueTeam = await rescueTeamRepository.GetByIdAsync(missionTeam.RescuerTeamId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy đội cứu hộ với ID: {missionTeam.RescuerTeamId}");

        rescueTeam.ReportIncident();
        await rescueTeamRepository.UpdateAsync(rescueTeam, cancellationToken);

        MissionActivityModel? currentActivity;
        if (request.MissionActivityId.HasValue)
        {
            currentActivity = await missionActivityRepository.GetByIdAsync(request.MissionActivityId.Value, cancellationToken)
                ?? throw new NotFoundException($"Không tìm thấy activity với ID: {request.MissionActivityId.Value}");

            if (currentActivity.MissionId != missionTeam.MissionId || currentActivity.MissionTeamId != request.MissionTeamId)
            {
                throw new BadRequestException(
                    $"Activity #{request.MissionActivityId.Value} không thuộc MissionTeamId={request.MissionTeamId}.");
            }

            if (currentActivity.Status != MissionActivityStatus.OnGoing)
            {
                throw new BadRequestException(
                    $"Activity #{request.MissionActivityId.Value} không ở trạng thái OnGoing để có thể báo incident.");
            }
        }
        else
        {
            currentActivity = (await missionActivityRepository.GetByMissionIdAsync(missionTeam.MissionId, cancellationToken))
                .Where(activity => activity.MissionTeamId == request.MissionTeamId
                    && activity.Status == MissionActivityStatus.OnGoing)
                .OrderBy(activity => activity.Step ?? int.MaxValue)
                .ThenBy(activity => activity.Id)
                .FirstOrDefault();
        }

        if (currentActivity is not null)
        {
            await mediator.Send(
                new UpdateActivityStatusCommand(missionTeam.MissionId, currentActivity.Id, MissionActivityStatus.Failed, request.ReportedBy),
                cancellationToken);
        }
        else
        {
            await unitOfWork.SaveAsync();
        }

        return new ReportTeamIncidentResponse
        {
            IncidentId = incidentId,
            MissionTeamId = request.MissionTeamId,
            Status = "Reported",
            ReportedAt = now
        };
    }
}
