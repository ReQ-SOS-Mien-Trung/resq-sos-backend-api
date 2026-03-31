using Microsoft.EntityFrameworkCore;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Identity;
using RESQ.Domain.Entities.Identity;
using RESQ.Infrastructure.Entities.Identity;
using RESQ.Infrastructure.Entities.Operations;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Infrastructure.Persistence.Identity;

public class RescuerScoreRepository(IUnitOfWork unitOfWork) : IRescuerScoreRepository
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<RescuerScoreModel?> GetByRescuerIdAsync(Guid rescuerId, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<RescuerScore>()
            .GetByPropertyAsync(x => x.UserId == rescuerId, tracked: false);

        return entity is null ? null : ToModel(entity);
    }

    public async Task<IDictionary<Guid, RescuerScoreModel>> GetByRescuerIdsAsync(IEnumerable<Guid> rescuerIds, CancellationToken cancellationToken = default)
    {
        var ids = rescuerIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, RescuerScoreModel>();
        }

        var entities = await _unitOfWork.GetRepository<RescuerScore>()
            .AsQueryable()
            .Where(x => ids.Contains(x.UserId))
            .ToListAsync(cancellationToken);

        return entities.ToDictionary(x => x.UserId, ToModel);
    }

    public async Task RefreshAsync(IEnumerable<Guid> rescuerIds, CancellationToken cancellationToken = default)
    {
        var ids = rescuerIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return;
        }

        var aggregates = await _unitOfWork.GetRepository<MissionTeamMemberEvaluation>()
            .AsQueryable()
            .Where(x =>
                ids.Contains(x.RescuerId) &&
                x.MissionTeamReport != null &&
                x.MissionTeamReport.ReportStatus == MissionTeamReportStatus.Submitted.ToString())
            .GroupBy(x => x.RescuerId)
            .Select(group => new
            {
                RescuerId = group.Key,
                EvaluationCount = group.Count(),
                ResponseTimeScore = group.Average(x => x.ResponseTimeScore),
                RescueEffectivenessScore = group.Average(x => x.RescueEffectivenessScore),
                DecisionHandlingScore = group.Average(x => x.DecisionHandlingScore),
                SafetyMedicalSkillScore = group.Average(x => x.SafetyMedicalSkillScore),
                TeamworkCommunicationScore = group.Average(x => x.TeamworkCommunicationScore)
            })
            .ToListAsync(cancellationToken);

        var aggregateByRescuer = aggregates.ToDictionary(x => x.RescuerId);

        var existingScores = await _unitOfWork.GetRepository<RescuerScore>()
            .AsQueryable(tracked: true)
            .Where(x => ids.Contains(x.UserId))
            .ToListAsync(cancellationToken);

        var existingByRescuer = existingScores.ToDictionary(x => x.UserId);
        var profileIds = await _unitOfWork.GetRepository<RescuerProfile>()
            .AsQueryable()
            .Where(x => ids.Contains(x.UserId))
            .Select(x => x.UserId)
            .ToListAsync(cancellationToken);

        var existingProfileIds = profileIds.ToHashSet();
        var now = DateTime.UtcNow;

        foreach (var rescuerId in ids)
        {
            if (!aggregateByRescuer.TryGetValue(rescuerId, out var aggregate))
            {
                if (existingByRescuer.TryGetValue(rescuerId, out var staleScore))
                {
                    staleScore.ResponseTimeScore = 0m;
                    staleScore.RescueEffectivenessScore = 0m;
                    staleScore.DecisionHandlingScore = 0m;
                    staleScore.SafetyMedicalSkillScore = 0m;
                    staleScore.TeamworkCommunicationScore = 0m;
                    staleScore.OverallAverageScore = 0m;
                    staleScore.EvaluationCount = 0;
                    staleScore.UpdatedAt = now;
                }

                continue;
            }

            if (!existingByRescuer.TryGetValue(rescuerId, out var entity))
            {
                if (!existingProfileIds.Contains(rescuerId))
                {
                    continue;
                }

                entity = new RescuerScore
                {
                    UserId = rescuerId,
                    CreatedAt = now
                };
                await _unitOfWork.GetRepository<RescuerScore>().AddAsync(entity);
                existingByRescuer[rescuerId] = entity;
            }

            entity.ResponseTimeScore = RoundScore(aggregate.ResponseTimeScore);
            entity.RescueEffectivenessScore = RoundScore(aggregate.RescueEffectivenessScore);
            entity.DecisionHandlingScore = RoundScore(aggregate.DecisionHandlingScore);
            entity.SafetyMedicalSkillScore = RoundScore(aggregate.SafetyMedicalSkillScore);
            entity.TeamworkCommunicationScore = RoundScore(aggregate.TeamworkCommunicationScore);
            entity.OverallAverageScore = RoundScore(
                (aggregate.ResponseTimeScore
                 + aggregate.RescueEffectivenessScore
                 + aggregate.DecisionHandlingScore
                 + aggregate.SafetyMedicalSkillScore
                 + aggregate.TeamworkCommunicationScore) / 5m);
            entity.EvaluationCount = aggregate.EvaluationCount;
            entity.UpdatedAt = now;
        }

        await _unitOfWork.SaveAsync();
    }

    private static RescuerScoreModel ToModel(RescuerScore entity) => new()
    {
        UserId = entity.UserId,
        ResponseTimeScore = entity.ResponseTimeScore,
        RescueEffectivenessScore = entity.RescueEffectivenessScore,
        DecisionHandlingScore = entity.DecisionHandlingScore,
        SafetyMedicalSkillScore = entity.SafetyMedicalSkillScore,
        TeamworkCommunicationScore = entity.TeamworkCommunicationScore,
        OverallAverageScore = entity.OverallAverageScore,
        EvaluationCount = entity.EvaluationCount,
        UpdatedAt = entity.UpdatedAt
    };

    private static decimal RoundScore(decimal value)
    {
        return Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}
