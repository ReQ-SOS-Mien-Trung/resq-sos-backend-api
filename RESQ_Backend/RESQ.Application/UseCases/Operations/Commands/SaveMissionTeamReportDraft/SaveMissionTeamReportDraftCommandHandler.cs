using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.UseCases.Operations.Queries.GetMissionTeamReport;
using RESQ.Application.UseCases.Operations.Shared;
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
            ?? throw new NotFoundException($"KhÃ´ng tÃ¬m tháº¥y mission vá»›i ID: {request.MissionId}");

        var missionTeam = await missionTeamRepository.GetByIdAsync(request.MissionTeamId, cancellationToken)
            ?? throw new NotFoundException($"KhÃ´ng tÃ¬m tháº¥y liÃªn káº¿t Ä‘á»™i-mission vá»›i ID: {request.MissionTeamId}");

        if (missionTeam.MissionId != request.MissionId)
            throw new BadRequestException("Mission team khÃ´ng thuá»™c mission Ä‘Æ°á»£c yÃªu cáº§u.");

        if (!missionTeam.RescueTeamMembers.Any(x => x.UserId == request.SavedBy))
            throw new ForbiddenException("Báº¡n khÃ´ng pháº£i thÃ nh viÃªn cá»§a Ä‘á»™i cá»©u há»™ nÃ y.");

        if (string.Equals(missionTeam.Status, MissionTeamExecutionStatus.Cancelled.ToString(), StringComparison.OrdinalIgnoreCase))
            throw new BadRequestException("Äá»™i Ä‘Ã£ bá»‹ há»§y phÃ¢n cÃ´ng, khÃ´ng thá»ƒ lÆ°u bÃ¡o cÃ¡o.");

        if (string.Equals(missionTeam.ReportStatus, MissionTeamReportStatus.Submitted.ToString(), StringComparison.OrdinalIgnoreCase)
            || string.Equals(missionTeam.Status, MissionTeamExecutionStatus.Reported.ToString(), StringComparison.OrdinalIgnoreCase))
            throw new ConflictException("BÃ¡o cÃ¡o cuá»‘i cÃ¹ng Ä‘Ã£ Ä‘Æ°á»£c ná»™p, khÃ´ng thá»ƒ lÆ°u nhÃ¡p ná»¯a.");

        var assignedActivities = mission.Activities
            .Where(x => x.MissionTeamId == request.MissionTeamId)
            .ToDictionary(x => x.Id);

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

        MissionTeamMemberEvaluationHelper.ValidateDraft(memberEvaluations, missionTeam, request.SavedBy);

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

        var report = await missionTeamReportRepository.GetByMissionTeamIdAsync(request.MissionTeamId, cancellationToken);
        return MissionTeamReportResponseFactory.Create(request.MissionId, missionTeam, report, assignedActivities.Values, request.SavedBy);
    }
}
