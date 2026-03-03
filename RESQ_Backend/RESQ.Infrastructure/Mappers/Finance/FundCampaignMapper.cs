using RESQ.Domain.Entities.Finance;
using RESQ.Domain.Enum.Finance;
using RESQ.Infrastructure.Entities.Finance;

namespace RESQ.Infrastructure.Mappers.Finance;

public static class FundCampaignMapper
{
    public static FundCampaignModel ToModel(FundCampaign entity)
    {
        var statusEnum = FundCampaignStatus.Active;
        if (!string.IsNullOrEmpty(entity.Status))
        {
            Enum.TryParse(entity.Status, true, out statusEnum);
        }

        // Use Factory Method to bypass private setters for hydration
        return FundCampaignModel.Reconstitute(
            entity.Id,
            entity.Code,
            entity.Name ?? string.Empty,
            entity.Region ?? string.Empty,
            entity.CampaignStartDate,
            entity.CampaignEndDate,
            entity.TargetAmount,
            entity.TotalAmount,
            statusEnum,
            entity.CreatedBy,
            entity.CreatedAt,
            entity.LastModifiedBy,
            entity.LastModifiedAt,
            entity.IsDeleted
        );
    }

    public static FundCampaign ToEntity(FundCampaignModel model)
    {
        return new FundCampaign
        {
            Id = model.Id,
            Code = model.Code,
            Name = model.Name,
            Region = model.Region,
            CampaignStartDate = model.Duration?.StartDate,
            CampaignEndDate = model.Duration?.EndDate,
            TargetAmount = model.TargetAmount,
            TotalAmount = model.TotalAmount,
            Status = model.Status.ToString(),
            CreatedBy = model.CreatedBy,
            CreatedAt = model.CreatedAt,
            LastModifiedBy = model.LastModifiedBy,
            LastModifiedAt = model.LastModifiedAt,
            IsDeleted = model.IsDeleted
        };
    }
    
    public static void UpdateEntity(FundCampaign entity, FundCampaignModel model)
    {
        entity.Name = model.Name;
        entity.Region = model.Region;
        entity.CampaignStartDate = model.Duration?.StartDate;
        entity.CampaignEndDate = model.Duration?.EndDate;
        entity.TargetAmount = model.TargetAmount;
        entity.TotalAmount = model.TotalAmount;
        entity.Status = model.Status.ToString();
        entity.LastModifiedBy = model.LastModifiedBy;
        entity.LastModifiedAt = model.LastModifiedAt;
        entity.IsDeleted = model.IsDeleted;
    }
}
