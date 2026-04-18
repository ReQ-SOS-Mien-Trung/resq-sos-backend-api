using RESQ.Domain.Entities.Finance;
using RESQ.Domain.Enum.Finance;
using RESQ.Infrastructure.Entities.Finance;

namespace RESQ.Infrastructure.Mappers.Finance;

public static class CampaignDisbursementMapper
{
    public static CampaignDisbursementModel ToModel(CampaignDisbursement entity)
    {
        var typeEnum = DisbursementType.AdminAllocation;
        if (!string.IsNullOrEmpty(entity.Type))
        {
            Enum.TryParse(entity.Type, true, out typeEnum);
        }

        var items = entity.DisbursementItems?.Select(i => new DisbursementItemModel
        {
            Id = i.Id,
            CampaignDisbursementId = i.CampaignDisbursementId,
            ItemName = i.ItemName,
            Unit = i.Unit,
            Quantity = i.Quantity,
            UnitPrice = i.UnitPrice,
            TotalPrice = i.TotalPrice,
            Note = i.Note,
            CreatedAt = i.CreatedAt
        }).ToList();

        var model = CampaignDisbursementModel.Reconstitute(
            entity.Id,
            entity.FundCampaignId,
            entity.DepotId,
            entity.Amount,
            entity.Purpose,
            typeEnum,
            entity.FundingRequestId,
            entity.CreatedBy,
            entity.CreatedAt,
            items
        );

        model.FundCampaignName = entity.FundCampaign?.Name;
        model.DepotName = entity.Depot?.Name;
        model.CreatedByUserName = entity.CreatedByUser?.Username;

        return model;
    }

    public static CampaignDisbursement ToEntity(CampaignDisbursementModel model)
    {
        var entity = new CampaignDisbursement
        {
            Id = model.Id,
            FundCampaignId = model.FundCampaignId,
            DepotId = model.DepotId,
            Amount = model.Amount,
            Purpose = model.Purpose,
            Type = model.Type.ToString(),
            FundingRequestId = model.FundingRequestId,
            CreatedBy = model.CreatedBy,
            CreatedAt = model.CreatedAt
        };

        foreach (var item in model.Items)
        {
            entity.DisbursementItems.Add(new DisbursementItem
            {
                CampaignDisbursementId = model.Id,
                ItemName = item.ItemName,
                Unit = item.Unit,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                TotalPrice = item.TotalPrice,
                Note = item.Note,
                CreatedAt = item.CreatedAt
            });
        }

        return entity;
    }
}
