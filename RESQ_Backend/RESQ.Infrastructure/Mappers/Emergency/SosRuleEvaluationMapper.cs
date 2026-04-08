using RESQ.Domain.Entities.Emergency;
using RESQ.Domain.Enum.Emergency;
using RESQ.Infrastructure.Entities.Emergency;

namespace RESQ.Infrastructure.Mappers.Emergency;

public static class SosRuleEvaluationMapper
{
    public static SosRuleEvaluation ToEntity(SosRuleEvaluationModel model)
    {
        return new SosRuleEvaluation
        {
            SosRequestId = model.SosRequestId,
            ConfigId = model.ConfigId,
            ConfigVersion = model.ConfigVersion,
            MedicalScore = model.MedicalScore,
            FoodScore = model.FoodScore,
            InjuryScore = model.InjuryScore,
            MobilityScore = model.MobilityScore,
            EnvironmentScore = model.EnvironmentScore,
            TotalScore = model.TotalScore,
            PriorityLevel = model.PriorityLevel.ToString(),
            RuleVersion = model.RuleVersion,
            ItemsNeeded = model.ItemsNeeded,
            BreakdownJson = model.BreakdownJson,
            DetailsJson = model.DetailsJson,
            CreatedAt = model.CreatedAt
        };
    }

    public static SosRuleEvaluationModel ToDomain(SosRuleEvaluation entity)
    {
        return new SosRuleEvaluationModel
        {
            Id = entity.Id,
            SosRequestId = entity.SosRequestId ?? 0,
            ConfigId = entity.ConfigId,
            ConfigVersion = entity.ConfigVersion,
            MedicalScore = entity.MedicalScore ?? 0,
            FoodScore = entity.FoodScore ?? 0,
            InjuryScore = entity.InjuryScore ?? 0,
            MobilityScore = entity.MobilityScore ?? 0,
            EnvironmentScore = entity.EnvironmentScore ?? 0,
            TotalScore = entity.TotalScore ?? 0,
            PriorityLevel = Enum.TryParse<SosPriorityLevel>(entity.PriorityLevel, out var priority) ? priority : SosPriorityLevel.Low,
            RuleVersion = entity.RuleVersion ?? "1.0",
            ItemsNeeded = entity.ItemsNeeded,
            BreakdownJson = entity.BreakdownJson,
            DetailsJson = entity.DetailsJson,
            CreatedAt = entity.CreatedAt ?? DateTime.UtcNow
        };
    }
}
