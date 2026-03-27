using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.UseCases.Operations.Queries.GetMissionTeamReport;
using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Enum.Emergency;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Application.UseCases.Operations.Commands.SubmitMissionTeamReport;

public class SubmitMissionTeamReportCommandHandler(
    IMissionRepository missionRepository,
    IMissionTeamRepository missionTeamRepository,
    IMissionTeamReportRepository missionTeamReportRepository,
    ISosRequestRepository sosRequestRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<SubmitMissionTeamReportCommand, MissionTeamReportResponse>
{
    public async Task<MissionTeamReportResponse> Handle(SubmitMissionTeamReportCommand request, CancellationToken cancellationToken)
    {
        var mission = await missionRepository.GetByIdAsync(request.MissionId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy mission với ID: {request.MissionId}");

        var missionTeam = await missionTeamRepository.GetByIdAsync(request.MissionTeamId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy liên kết đội-mission với ID: {request.MissionTeamId}");

        if (missionTeam.MissionId != request.MissionId)
            throw new BadRequestException("Mission team không thuộc mission được yêu cầu.");

        var leader = missionTeam.RescueTeamMembers.FirstOrDefault(x => x.UserId == request.SubmittedBy && x.IsLeader);
        if (leader is null)
            throw new ForbiddenException("Chỉ đội trưởng mới có quyền nộp báo cáo cuối cùng.");

        if (string.Equals(missionTeam.Status, MissionTeamExecutionStatus.Cancelled.ToString(), StringComparison.OrdinalIgnoreCase))
            throw new BadRequestException("Đội đã bị hủy phân công, không thể nộp báo cáo.");

        if (!string.Equals(missionTeam.Status, MissionTeamExecutionStatus.CompletedWaitingReport.ToString(), StringComparison.OrdinalIgnoreCase))
            throw new BadRequestException("Đội phải hoàn tất thực thi trước khi nộp báo cáo cuối cùng.");

        if (string.Equals(missionTeam.ReportStatus, MissionTeamReportStatus.Submitted.ToString(), StringComparison.OrdinalIgnoreCase)
            || string.Equals(missionTeam.Status, MissionTeamExecutionStatus.Reported.ToString(), StringComparison.OrdinalIgnoreCase))
            throw new ConflictException("Báo cáo cuối cùng đã được nộp trước đó.");

        var assignedActivities = mission.Activities
            .Where(x => x.MissionTeamId == request.MissionTeamId)
            .ToDictionary(x => x.Id);

        if (assignedActivities.Count == 0)
            throw new BadRequestException("Đội này chưa được giao activity nào để báo cáo.");

        var invalidActivityId = request.Activities
            .Select(x => x.MissionActivityId)
            .FirstOrDefault(id => !assignedActivities.ContainsKey(id));

        if (invalidActivityId > 0)
            throw new BadRequestException($"Activity #{invalidActivityId} không thuộc mission team này.");

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
                }).ToList()
            }, cancellationToken);

            await missionTeamReportRepository.SubmitAsync(request.MissionTeamId, request.SubmittedBy, cancellationToken);
            await missionTeamRepository.UpdateStatusAsync(request.MissionTeamId, MissionTeamExecutionStatus.Reported.ToString(), cancellationToken);

            var refreshedTeams = (await missionTeamRepository.GetByMissionIdAsync(request.MissionId, cancellationToken))
                .Where(x => !string.Equals(x.Status, MissionTeamExecutionStatus.Cancelled.ToString(), StringComparison.OrdinalIgnoreCase))
                .ToList();

            var allReported = refreshedTeams.Count > 0 && refreshedTeams.All(x => string.Equals(x.Status, MissionTeamExecutionStatus.Reported.ToString(), StringComparison.OrdinalIgnoreCase));
            if (allReported)
            {
                await missionRepository.UpdateStatusAsync(request.MissionId, MissionStatus.Completed, isCompleted: true, cancellationToken);
                if (mission.ClusterId.HasValue)
                {
                    await sosRequestRepository.UpdateStatusByClusterIdAsync(mission.ClusterId.Value, SosRequestStatus.Resolved, cancellationToken);
                }
            }
        });

        var refreshedMissionTeam = await missionTeamRepository.GetByIdAsync(request.MissionTeamId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy liên kết đội-mission với ID: {request.MissionTeamId}");
        var report = await missionTeamReportRepository.GetByMissionTeamIdAsync(request.MissionTeamId, cancellationToken);

        return MissionTeamReportResponseFactory.Create(request.MissionId, refreshedMissionTeam, report, assignedActivities.Values, request.SubmittedBy);
    }
}