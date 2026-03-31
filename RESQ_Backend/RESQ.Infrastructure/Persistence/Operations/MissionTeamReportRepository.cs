using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Operations;
using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Enum.Operations;
using RESQ.Infrastructure.Entities.Operations;

namespace RESQ.Infrastructure.Persistence.Operations;

public class MissionTeamReportRepository(IUnitOfWork unitOfWork) : IMissionTeamReportRepository
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<MissionTeamReportModel?> GetByMissionTeamIdAsync(int missionTeamId, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<MissionTeamReport>()
            .GetByPropertyAsync(
                r => r.MissionTeamId == missionTeamId,
                tracked: false,
                includeProperties: "MissionActivityReports,MissionTeamMemberEvaluations");

        return entity is null ? null : ToModel(entity);
    }

    public async Task<int> UpsertDraftAsync(MissionTeamReportModel model, CancellationToken cancellationToken = default)
    {
        var repository = _unitOfWork.GetRepository<MissionTeamReport>();
        var entity = await repository.GetByPropertyAsync(
            r => r.MissionTeamId == model.MissionTeamId,
            tracked: true,
            includeProperties: "MissionActivityReports,MissionTeamMemberEvaluations");

        if (entity is null)
        {
            entity = new MissionTeamReport
            {
                MissionTeamId = model.MissionTeamId,
                CreatedAt = DateTime.UtcNow
            };
            await repository.AddAsync(entity);
        }

        entity.ReportStatus = MissionTeamReportStatus.Draft.ToString();
        entity.TeamSummary = model.TeamSummary;
        entity.TeamNote = model.TeamNote;
        entity.IssuesJson = model.IssuesJson;
        entity.ResultJson = model.ResultJson;
        entity.EvidenceJson = model.EvidenceJson;
        entity.StartedAt = model.StartedAt ?? entity.StartedAt ?? DateTime.UtcNow;
        entity.LastEditedAt = model.LastEditedAt ?? DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;

        await SyncActivityReportsAsync(entity, model.ActivityReports);
        await SyncMemberEvaluationsAsync(entity, model.MemberEvaluations);

        await _unitOfWork.SaveAsync();
        return entity.Id;
    }

    public async Task SubmitAsync(int missionTeamId, Guid submittedBy, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<MissionTeamReport>()
            .GetByPropertyAsync(r => r.MissionTeamId == missionTeamId, tracked: true);

        if (entity is null)
        {
            throw new InvalidOperationException($"KhÃ´ng tÃ¬m tháº¥y bÃ¡o cÃ¡o cho mission team {missionTeamId}.");
        }

        entity.ReportStatus = MissionTeamReportStatus.Submitted.ToString();
        entity.SubmittedBy = submittedBy;
        entity.SubmittedAt = DateTime.UtcNow;
        entity.LastEditedAt = entity.SubmittedAt;
        entity.UpdatedAt = entity.SubmittedAt;

        await _unitOfWork.SaveAsync();
    }

    public async Task UpdateReportStatusAsync(int missionTeamId, MissionTeamReportStatus status, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<MissionTeamReport>()
            .GetByPropertyAsync(r => r.MissionTeamId == missionTeamId, tracked: true);

        if (entity is null)
        {
            return;
        }

        entity.ReportStatus = status.ToString();
        entity.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.SaveAsync();
    }

    private async Task SyncActivityReportsAsync(MissionTeamReport entity, IEnumerable<MissionActivityReportModel> models)
    {
        var reportRepository = _unitOfWork.GetRepository<MissionActivityReport>();
        var incoming = models.GroupBy(x => x.MissionActivityId).ToDictionary(g => g.Key, g => g.First());

        foreach (var existing in entity.MissionActivityReports.ToList())
        {
            if (incoming.ContainsKey(existing.MissionActivityId))
            {
                continue;
            }

            entity.MissionActivityReports.Remove(existing);
            await reportRepository.DeleteAsyncById(existing.Id);
        }

        foreach (var model in models)
        {
            var existing = entity.MissionActivityReports.FirstOrDefault(x => x.MissionActivityId == model.MissionActivityId);
            if (existing is null)
            {
                existing = new MissionActivityReport
                {
                    MissionActivityId = model.MissionActivityId,
                    CreatedAt = DateTime.UtcNow
                };
                entity.MissionActivityReports.Add(existing);
            }

            existing.ActivityCode = model.ActivityCode;
            existing.ActivityType = model.ActivityType;
            existing.ExecutionStatus = model.ExecutionStatus;
            existing.Summary = model.Summary;
            existing.IssuesJson = model.IssuesJson;
            existing.ResultJson = model.ResultJson;
            existing.EvidenceJson = model.EvidenceJson;
            existing.UpdatedAt = DateTime.UtcNow;
        }
    }

    private async Task SyncMemberEvaluationsAsync(MissionTeamReport entity, IEnumerable<MissionTeamMemberEvaluationModel> models)
    {
        var evaluationRepository = _unitOfWork.GetRepository<MissionTeamMemberEvaluation>();
        var incoming = models.GroupBy(x => x.RescuerId).ToDictionary(g => g.Key, g => g.First());

        foreach (var existing in entity.MissionTeamMemberEvaluations.ToList())
        {
            if (incoming.ContainsKey(existing.RescuerId))
            {
                continue;
            }

            entity.MissionTeamMemberEvaluations.Remove(existing);
            await evaluationRepository.DeleteAsyncById(existing.Id);
        }

        foreach (var model in models)
        {
            var existing = entity.MissionTeamMemberEvaluations.FirstOrDefault(x => x.RescuerId == model.RescuerId);
            if (existing is null)
            {
                existing = new MissionTeamMemberEvaluation
                {
                    RescuerId = model.RescuerId,
                    CreatedAt = DateTime.UtcNow
                };
                entity.MissionTeamMemberEvaluations.Add(existing);
            }

            existing.ResponseTimeScore = model.ResponseTimeScore;
            existing.RescueEffectivenessScore = model.RescueEffectivenessScore;
            existing.DecisionHandlingScore = model.DecisionHandlingScore;
            existing.SafetyMedicalSkillScore = model.SafetyMedicalSkillScore;
            existing.TeamworkCommunicationScore = model.TeamworkCommunicationScore;
            existing.UpdatedAt = DateTime.UtcNow;
        }
    }

    private static MissionTeamReportModel ToModel(MissionTeamReport entity) => new()
    {
        Id = entity.Id,
        MissionTeamId = entity.MissionTeamId,
        ReportStatus = Enum.TryParse<MissionTeamReportStatus>(entity.ReportStatus, out var reportStatus)
            ? reportStatus
            : MissionTeamReportStatus.NotStarted,
        TeamSummary = entity.TeamSummary,
        TeamNote = entity.TeamNote,
        IssuesJson = entity.IssuesJson,
        ResultJson = entity.ResultJson,
        EvidenceJson = entity.EvidenceJson,
        StartedAt = entity.StartedAt,
        LastEditedAt = entity.LastEditedAt,
        SubmittedAt = entity.SubmittedAt,
        SubmittedBy = entity.SubmittedBy,
        ActivityReports = entity.MissionActivityReports
            .OrderBy(x => x.MissionActivityId)
            .Select(x => new MissionActivityReportModel
            {
                Id = x.Id,
                MissionTeamReportId = x.MissionTeamReportId,
                MissionActivityId = x.MissionActivityId,
                ActivityCode = x.ActivityCode,
                ActivityType = x.ActivityType,
                ExecutionStatus = x.ExecutionStatus,
                Summary = x.Summary,
                IssuesJson = x.IssuesJson,
                ResultJson = x.ResultJson,
                EvidenceJson = x.EvidenceJson,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            })
            .ToList(),
        MemberEvaluations = entity.MissionTeamMemberEvaluations
            .OrderBy(x => x.RescuerId)
            .Select(x => new MissionTeamMemberEvaluationModel
            {
                Id = x.Id,
                MissionTeamReportId = x.MissionTeamReportId,
                RescuerId = x.RescuerId,
                ResponseTimeScore = x.ResponseTimeScore,
                RescueEffectivenessScore = x.RescueEffectivenessScore,
                DecisionHandlingScore = x.DecisionHandlingScore,
                SafetyMedicalSkillScore = x.SafetyMedicalSkillScore,
                TeamworkCommunicationScore = x.TeamworkCommunicationScore,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            })
            .ToList()
    };
}
