using MediatR;
using RESQ.Application.Common.Constants;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.Services;

namespace RESQ.Application.UseCases.Operations.Queries.GetMissionTeamReport;

public class GetMissionTeamReportQueryHandler(
    IMissionRepository missionRepository,
    IMissionTeamRepository missionTeamRepository,
    IMissionTeamReportRepository missionTeamReportRepository,
    IUserPermissionResolver permissionResolver)
    : IRequestHandler<GetMissionTeamReportQuery, MissionTeamReportResponse>
{
    public async Task<MissionTeamReportResponse> Handle(GetMissionTeamReportQuery request, CancellationToken cancellationToken)
    {
        var mission = await missionRepository.GetByIdAsync(request.MissionId, cancellationToken)
            ?? throw new NotFoundException($"KhÃ´ng tÃ¬m tháº¥y mission vá»›i ID: {request.MissionId}");

        var missionTeam = await missionTeamRepository.GetByIdAsync(request.MissionTeamId, cancellationToken)
            ?? throw new NotFoundException($"KhÃ´ng tÃ¬m tháº¥y liÃªn káº¿t Ä‘á»™i-mission vá»›i ID: {request.MissionTeamId}");

        if (missionTeam.MissionId != request.MissionId)
            throw new BadRequestException("Mission team khÃ´ng thuá»™c mission Ä‘Æ°á»£c yÃªu cáº§u.");

        var permissionCodes = await permissionResolver.GetEffectivePermissionCodesAsync(request.RequestedBy, cancellationToken);
        var hasSystemUserView = permissionCodes.Contains(PermissionConstants.SystemUserView, StringComparer.OrdinalIgnoreCase);

        if (!hasSystemUserView && !missionTeam.RescueTeamMembers.Any(x => x.UserId == request.RequestedBy))
            throw new ForbiddenException("Báº¡n khÃ´ng pháº£i thÃ nh viÃªn cá»§a Ä‘á»™i cá»©u há»™ nÃ y.");

        var report = await missionTeamReportRepository.GetByMissionTeamIdAsync(request.MissionTeamId, cancellationToken);
        var assignedActivities = mission.Activities.Where(x => x.MissionTeamId == request.MissionTeamId).ToList();

        return MissionTeamReportResponseFactory.Create(
            request.MissionId,
            missionTeam,
            report,
            assignedActivities,
            request.RequestedBy,
            isReadOnlyViewer: hasSystemUserView);
    }
}
