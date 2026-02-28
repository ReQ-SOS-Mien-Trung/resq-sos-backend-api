using RESQ.Domain.Entities.Finance;
using RESQ.Domain.Enum.Finance;
using RESQ.Infrastructure.Entities.Finance;

namespace RESQ.Infrastructure.Mappers.Finance;

public static class FundCampaignMapper
{
    public static FundCampaignModel ToModel(FundCampaign entity)
    {
        // Default to Active if parsing fails or null
        var statusEnum = FundCampaignStatus.Active;
        if (!string.IsNullOrEmpty(entity.Status))
        {
            Enum.TryParse(entity.Status, true, out statusEnum);
        }

        return new FundCampaignModel
        {
            Id = entity.Id,
            Code = entity.Code,
            Name = entity.Name,
            Region = entity.Region,
            CampaignStartDate = entity.CampaignStartDate,
            CampaignEndDate = entity.CampaignEndDate,
            TargetAmount = entity.TargetAmount,
            TotalAmount = entity.TotalAmount,
            Status = statusEnum,
            CreatedBy = entity.CreatedBy,
            CreatedAt = entity.CreatedAt
        };
    }

    public static FundCampaign ToEntity(FundCampaignModel model)
    {
        return new FundCampaign
        {
            Id = model.Id,
            Code = model.Code,
            Name = model.Name,
            Region = model.Region,
            CampaignStartDate = model.CampaignStartDate,
            CampaignEndDate = model.CampaignEndDate,
            TargetAmount = model.TargetAmount,
            TotalAmount = model.TotalAmount,
            // Map Enum to PascalCase string: Active | Closed | Archived
            Status = model.Status.ToString(),
            CreatedBy = model.CreatedBy,
            CreatedAt = model.CreatedAt
        };
    }
    
    public static void UpdateEntity(FundCampaign entity, FundCampaignModel model)
    {
        entity.Code = model.Code;
        entity.Name = model.Name;
        entity.Region = model.Region;
        entity.CampaignStartDate = model.CampaignStartDate;
        entity.CampaignEndDate = model.CampaignEndDate;
        entity.TargetAmount = model.TargetAmount;
        entity.TotalAmount = model.TotalAmount;
        entity.Status = model.Status.ToString();
    }
}
