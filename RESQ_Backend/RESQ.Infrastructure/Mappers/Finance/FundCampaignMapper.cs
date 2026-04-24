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
            entity.CurrentBalance,
            statusEnum,
            entity.SuspendReason,
            entity.CreatedBy,
            entity.CreatedAt,
            entity.LastModifiedBy,
            entity.LastModifiedAt,
            entity.IsDeleted,
            entity.RowVersion
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
            CurrentBalance = model.CurrentBalance,
            Status = model.Status.ToString(),
            SuspendReason = model.SuspendReason,
            CreatedBy = model.CreatedBy,
            CreatedAt = model.CreatedAt,
            LastModifiedBy = model.LastModifiedBy,
            LastModifiedAt = model.LastModifiedAt,
            IsDeleted = model.IsDeleted,
            RowVersion = model.RowVersion
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
        entity.CurrentBalance = model.CurrentBalance;
        entity.Status = model.Status.ToString();
        entity.SuspendReason = model.SuspendReason;
        entity.LastModifiedBy = model.LastModifiedBy;
        entity.LastModifiedAt = model.LastModifiedAt;
        entity.IsDeleted = model.IsDeleted;
        entity.RowVersion = model.RowVersion;
    }
}
