using RESQ.Domain.Entities.Finance;
using RESQ.Domain.Enum.Finance;
using RESQ.Infrastructure.Entities.Finance;

namespace RESQ.Infrastructure.Mappers.Finance;

public static class FundTransactionMapper
{
    public static FundTransactionModel ToModel(FundTransaction entity)
    {
        // Parse Type
        var typeEnum = TransactionType.Donation;
        if (!string.IsNullOrEmpty(entity.Type))
        {
             Enum.TryParse(entity.Type, true, out typeEnum);
        }

        // Parse Reference Type
        var refTypeEnum = TransactionReferenceType.Donation;
        if (!string.IsNullOrEmpty(entity.ReferenceType))
        {
            Enum.TryParse(entity.ReferenceType, true, out refTypeEnum);
        }

        // Parse Direction
        var directionEnum = TransactionDirection.In;
        if (!string.IsNullOrEmpty(entity.Direction))
        {
            Enum.TryParse(entity.Direction, true, out directionEnum);
        }

        return new FundTransactionModel
        {
            Id = entity.Id,
            FundCampaignId = entity.FundCampaignId,
            Type = typeEnum,
            Direction = directionEnum,
            Amount = entity.Amount,
            ReferenceType = refTypeEnum,
            ReferenceId = entity.ReferenceId,
            CreatedBy = entity.CreatedBy,
            CreatedAt = entity.CreatedAt,
            FundCampaignName = entity.FundCampaign?.Name,
            CreatedByUserName = entity.CreatedByUser?.Username
        };
    }

    public static FundTransaction ToEntity(FundTransactionModel model)
    {
        return new FundTransaction
        {
            Id = model.Id,
            FundCampaignId = model.FundCampaignId,
            Type = model.Type.ToString(),
            Direction = model.Direction.ToString(),
            Amount = model.Amount,
            ReferenceType = model.ReferenceType.ToString(),
            ReferenceId = model.ReferenceId,
            CreatedBy = model.CreatedBy,
            CreatedAt = model.CreatedAt
        };
    }
}
