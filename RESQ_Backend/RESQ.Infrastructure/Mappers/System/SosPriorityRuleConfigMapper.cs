using RESQ.Domain.Entities.System;
using RESQ.Infrastructure.Entities.System;

namespace RESQ.Infrastructure.Mappers.System;

public static class SosPriorityRuleConfigMapper
{
    public static SosPriorityRuleConfig ToEntity(SosPriorityRuleConfigModel model)
    {
        var entity = new SosPriorityRuleConfig
        {
            ConfigVersion = model.ConfigVersion,
            IsActive = model.IsActive,
            CreatedAt = model.CreatedAt,
            CreatedBy = model.CreatedBy,
            ActivatedAt = model.ActivatedAt,
            ActivatedBy = model.ActivatedBy,
            ConfigJson = model.ConfigJson,
            IssueWeightsJson = model.IssueWeightsJson,
            MedicalSevereIssuesJson = model.MedicalSevereIssuesJson,
            AgeWeightsJson = model.AgeWeightsJson,
            RequestTypeScoresJson = model.RequestTypeScoresJson,
            SituationMultipliersJson = model.SituationMultipliersJson,
            PriorityThresholdsJson = model.PriorityThresholdsJson,
            WaterUrgencyScoresJson = model.WaterUrgencyScoresJson,
            FoodUrgencyScoresJson = model.FoodUrgencyScoresJson,
            BlanketUrgencyRulesJson = model.BlanketUrgencyRulesJson,
            ClothingUrgencyRulesJson = model.ClothingUrgencyRulesJson,
            VulnerabilityRulesJson = model.VulnerabilityRulesJson,
            VulnerabilityScoreExpressionJson = model.VulnerabilityScoreExpressionJson,
            ReliefScoreExpressionJson = model.ReliefScoreExpressionJson,
            PriorityScoreExpressionJson = model.PriorityScoreExpressionJson,
            UpdatedAt = model.UpdatedAt
        };

        if (model.Id > 0)
            entity.Id = model.Id;

        return entity;
    }

    public static SosPriorityRuleConfigModel ToDomain(SosPriorityRuleConfig entity)
    {
        return new SosPriorityRuleConfigModel
        {
            Id = entity.Id,
            ConfigVersion = entity.ConfigVersion,
            IsActive = entity.IsActive,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            ActivatedAt = entity.ActivatedAt,
            ActivatedBy = entity.ActivatedBy,
            ConfigJson = entity.ConfigJson,
            IssueWeightsJson = entity.IssueWeightsJson,
            MedicalSevereIssuesJson = entity.MedicalSevereIssuesJson,
            AgeWeightsJson = entity.AgeWeightsJson,
            RequestTypeScoresJson = entity.RequestTypeScoresJson,
            SituationMultipliersJson = entity.SituationMultipliersJson,
            PriorityThresholdsJson = entity.PriorityThresholdsJson,
            WaterUrgencyScoresJson = entity.WaterUrgencyScoresJson,
            FoodUrgencyScoresJson = entity.FoodUrgencyScoresJson,
            BlanketUrgencyRulesJson = entity.BlanketUrgencyRulesJson,
            ClothingUrgencyRulesJson = entity.ClothingUrgencyRulesJson,
            VulnerabilityRulesJson = entity.VulnerabilityRulesJson,
            VulnerabilityScoreExpressionJson = entity.VulnerabilityScoreExpressionJson,
            ReliefScoreExpressionJson = entity.ReliefScoreExpressionJson,
            PriorityScoreExpressionJson = entity.PriorityScoreExpressionJson,
            UpdatedAt = entity.UpdatedAt
        };
    }
}
