using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Operations;

namespace RESQ.Application.UseCases.Operations.Queries.GetMissionTeamReport;

public class GetMissionTeamReportQueryHandler(
    IMissionRepository missionRepository,
    IMissionTeamRepository missionTeamRepository,
    IMissionTeamReportRepository missionTeamReportRepository)
    : IRequestHandler<GetMissionTeamReportQuery, MissionTeamReportResponse>
{
    public async Task<MissionTeamReportResponse> Handle(GetMissionTeamReportQuery request, CancellationToken cancellationToken)
    {
        var mission = await missionRepository.GetByIdAsync(request.MissionId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy mission với ID: {request.MissionId}");

        var missionTeam = await missionTeamRepository.GetByIdAsync(request.MissionTeamId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy liên kết đội-mission với ID: {request.MissionTeamId}");

        if (missionTeam.MissionId != request.MissionId)
            throw new BadRequestException("Mission team không thuộc mission được yêu cầu.");

        if (!missionTeam.RescueTeamMembers.Any(x => x.UserId == request.RequestedBy))
            throw new ForbiddenException("Bạn không phải thành viên của đội cứu hộ này.");

        var report = await missionTeamReportRepository.GetByMissionTeamIdAsync(request.MissionTeamId, cancellationToken);
        var assignedActivities = mission.Activities.Where(x => x.MissionTeamId == request.MissionTeamId).ToList();

        return MissionTeamReportResponseFactory.Create(request.MissionId, missionTeam, report, assignedActivities, request.RequestedBy);
    }
}
