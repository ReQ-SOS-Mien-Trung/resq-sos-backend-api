using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.UseCases.Operations.Queries.GetMissionTeamReport;
using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Application.UseCases.Operations.Commands.SaveMissionTeamReportDraft;

public class SaveMissionTeamReportDraftCommandHandler(
    IMissionRepository missionRepository,
    IMissionTeamRepository missionTeamRepository,
    IMissionTeamReportRepository missionTeamReportRepository)
    : IRequestHandler<SaveMissionTeamReportDraftCommand, MissionTeamReportResponse>
{
    public async Task<MissionTeamReportResponse> Handle(SaveMissionTeamReportDraftCommand request, CancellationToken cancellationToken)
    {
        var mission = await missionRepository.GetByIdAsync(request.MissionId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy mission với ID: {request.MissionId}");

        var missionTeam = await missionTeamRepository.GetByIdAsync(request.MissionTeamId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy liên kết đội-mission với ID: {request.MissionTeamId}");

        if (missionTeam.MissionId != request.MissionId)
            throw new BadRequestException("Mission team không thuộc mission được yêu cầu.");

        if (!missionTeam.RescueTeamMembers.Any(x => x.UserId == request.SavedBy))
            throw new ForbiddenException("Bạn không phải thành viên của đội cứu hộ này.");

        if (string.Equals(missionTeam.Status, MissionTeamExecutionStatus.Cancelled.ToString(), StringComparison.OrdinalIgnoreCase))
            throw new BadRequestException("Đội đã bị hủy phân công, không thể lưu báo cáo.");

        if (string.Equals(missionTeam.ReportStatus, MissionTeamReportStatus.Submitted.ToString(), StringComparison.OrdinalIgnoreCase)
            || string.Equals(missionTeam.Status, MissionTeamExecutionStatus.Reported.ToString(), StringComparison.OrdinalIgnoreCase))
            throw new ConflictException("Báo cáo cuối cùng đã được nộp, không thể lưu nháp nữa.");

        var assignedActivities = mission.Activities
            .Where(x => x.MissionTeamId == request.MissionTeamId)
            .ToDictionary(x => x.Id);

        var invalidActivityId = request.Activities
            .Select(x => x.MissionActivityId)
            .FirstOrDefault(id => !assignedActivities.ContainsKey(id));

        if (invalidActivityId > 0)
            throw new BadRequestException($"Activity #{invalidActivityId} không thuộc mission team này.");

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
            }).ToList()
        }, cancellationToken);

        var report = await missionTeamReportRepository.GetByMissionTeamIdAsync(request.MissionTeamId, cancellationToken);
        return MissionTeamReportResponseFactory.Create(request.MissionId, missionTeam, report, assignedActivities.Values, request.SavedBy);
    }
}