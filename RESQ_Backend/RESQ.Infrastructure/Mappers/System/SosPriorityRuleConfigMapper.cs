using RESQ.Domain.Entities.System;
using RESQ.Infrastructure.Entities.System;

namespace RESQ.Infrastructure.Mappers.System;

public static class SosPriorityRuleConfigMapper
{
    public static SosPriorityRuleConfig ToEntity(SosPriorityRuleConfigModel model)
    {
        var entity = new SosPriorityRuleConfig
        {
            ConfigJson = model.ConfigJson,
            IssueWeightsJson = model.IssueWeightsJson,
            MedicalSevereIssuesJson = model.MedicalSevereIssuesJson,
            AgeWeightsJson = model.AgeWeightsJson,
            RequestTypeScoresJson = model.RequestTypeScoresJson,
            SituationMultipliersJson = model.SituationMultipliersJson,
            PriorityThresholdsJson = model.PriorityThresholdsJson,
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
            ConfigJson = entity.ConfigJson,
            IssueWeightsJson = entity.IssueWeightsJson,
            MedicalSevereIssuesJson = entity.MedicalSevereIssuesJson,
            AgeWeightsJson = entity.AgeWeightsJson,
            RequestTypeScoresJson = entity.RequestTypeScoresJson,
            SituationMultipliersJson = entity.SituationMultipliersJson,
            PriorityThresholdsJson = entity.PriorityThresholdsJson,
            UpdatedAt = entity.UpdatedAt
        };
    }
}
