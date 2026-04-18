using Microsoft.EntityFrameworkCore;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Identity;
using RESQ.Domain.Entities.Identity;
using RESQ.Domain.Entities.Operations;
using RESQ.Infrastructure.Entities.Identity;

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

    public async Task<RescuerScoreModel?> GetVisibleByRescuerIdAsync(Guid rescuerId, int minimumEvaluationCount, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<RescuerScore>()
            .AsQueryable()
            .Where(x => x.UserId == rescuerId && x.EvaluationCount >= minimumEvaluationCount)
            .FirstOrDefaultAsync(cancellationToken);

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

    public async Task<IDictionary<Guid, RescuerScoreModel>> GetVisibleByRescuerIdsAsync(IEnumerable<Guid> rescuerIds, int minimumEvaluationCount, CancellationToken cancellationToken = default)
    {
        var ids = rescuerIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, RescuerScoreModel>();
        }

        var entities = await _unitOfWork.GetRepository<RescuerScore>()
            .AsQueryable()
            .Where(x => ids.Contains(x.UserId) && x.EvaluationCount >= minimumEvaluationCount)
            .ToListAsync(cancellationToken);

        return entities.ToDictionary(x => x.UserId, ToModel);
    }

    public async Task RefreshAsync(IEnumerable<MissionTeamMemberEvaluationModel> newEvaluations, CancellationToken cancellationToken = default)
    {
        var evaluationList = newEvaluations.ToList();
        if (evaluationList.Count == 0)
        {
            return;
        }

        // Group new evaluations by rescuer to handle batch (multiple evals for same rescuer)
        var grouped = evaluationList
            .GroupBy(x => x.RescuerId)
            .Select(g => new
            {
                RescuerId = g.Key,
                Count = g.Count(),
                ResponseTimeScore = g.Average(x => x.ResponseTimeScore),
                RescueEffectivenessScore = g.Average(x => x.RescueEffectivenessScore),
                DecisionHandlingScore = g.Average(x => x.DecisionHandlingScore),
                SafetyMedicalSkillScore = g.Average(x => x.SafetyMedicalSkillScore),
                TeamworkCommunicationScore = g.Average(x => x.TeamworkCommunicationScore)
            })
            .ToList();

        var ids = grouped.Select(x => x.RescuerId).ToList();

        var existingScores = await _unitOfWork.GetRepository<RescuerScore>()
            .AsQueryable(tracked: true)
            .Where(x => ids.Contains(x.UserId))
            .ToListAsync(cancellationToken);

        var existingByRescuer = existingScores.ToDictionary(x => x.UserId);

        // Only check profiles for rescuers that don't have a score record yet
        var missingIds = ids.Where(id => !existingByRescuer.ContainsKey(id)).ToList();
        var existingProfileIds = new HashSet<Guid>();
        if (missingIds.Count > 0)
        {
            var profileIds = await _unitOfWork.GetRepository<RescuerProfile>()
                .AsQueryable()
                .Where(x => missingIds.Contains(x.UserId))
                .Select(x => x.UserId)
                .ToListAsync(cancellationToken);
            existingProfileIds = profileIds.ToHashSet();
        }

        var now = DateTime.UtcNow;

        foreach (var newScore in grouped)
        {
            if (!existingByRescuer.TryGetValue(newScore.RescuerId, out var entity))
            {
                if (!existingProfileIds.Contains(newScore.RescuerId))
                {
                    continue;
                }

                // First evaluation for this rescuer - create new record
                entity = new RescuerScore
                {
                    UserId = newScore.RescuerId,
                    ResponseTimeScore = RoundScore(newScore.ResponseTimeScore),
                    RescueEffectivenessScore = RoundScore(newScore.RescueEffectivenessScore),
                    DecisionHandlingScore = RoundScore(newScore.DecisionHandlingScore),
                    SafetyMedicalSkillScore = RoundScore(newScore.SafetyMedicalSkillScore),
                    TeamworkCommunicationScore = RoundScore(newScore.TeamworkCommunicationScore),
                    EvaluationCount = newScore.Count,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                entity.OverallAverageScore = RoundScore(
                    (entity.ResponseTimeScore
                     + entity.RescueEffectivenessScore
                     + entity.DecisionHandlingScore
                     + entity.SafetyMedicalSkillScore
                     + entity.TeamworkCommunicationScore) / 5m);

                await _unitOfWork.GetRepository<RescuerScore>().AddAsync(entity);
                continue;
            }

            // Incremental running average: NewAvg = (OldAvg * OldCount + NewSum) / (OldCount + NewCount)
            var oldCount = entity.EvaluationCount;
            var newCount = newScore.Count;
            var totalCount = oldCount + newCount;

            entity.ResponseTimeScore = RunningAverage(entity.ResponseTimeScore, oldCount, newScore.ResponseTimeScore, newCount, totalCount);
            entity.RescueEffectivenessScore = RunningAverage(entity.RescueEffectivenessScore, oldCount, newScore.RescueEffectivenessScore, newCount, totalCount);
            entity.DecisionHandlingScore = RunningAverage(entity.DecisionHandlingScore, oldCount, newScore.DecisionHandlingScore, newCount, totalCount);
            entity.SafetyMedicalSkillScore = RunningAverage(entity.SafetyMedicalSkillScore, oldCount, newScore.SafetyMedicalSkillScore, newCount, totalCount);
            entity.TeamworkCommunicationScore = RunningAverage(entity.TeamworkCommunicationScore, oldCount, newScore.TeamworkCommunicationScore, newCount, totalCount);
            entity.OverallAverageScore = RoundScore(
                (entity.ResponseTimeScore
                 + entity.RescueEffectivenessScore
                 + entity.DecisionHandlingScore
                 + entity.SafetyMedicalSkillScore
                 + entity.TeamworkCommunicationScore) / 5m);
            entity.EvaluationCount = totalCount;
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

    private static decimal RunningAverage(decimal oldAvg, int oldCount, decimal newAvg, int newCount, int totalCount)
    {
        if (totalCount == 0) return 0m;
        return RoundScore((oldAvg * oldCount + newAvg * newCount) / totalCount);
    }

    private static decimal RoundScore(decimal value)
    {
        return Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}
