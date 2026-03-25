using RESQ.Domain.Entities.Finance;
using RESQ.Domain.Enum.Finance;
using RESQ.Infrastructure.Entities.Finance;

namespace RESQ.Infrastructure.Mappers.Finance;

public static class FundingRequestMapper
{
    public static FundingRequestModel ToModel(FundingRequest entity)
    {
        var statusEnum = FundingRequestStatus.Pending;
        if (!string.IsNullOrEmpty(entity.Status))
        {
            Enum.TryParse(entity.Status, true, out statusEnum);
        }

        var items = entity.FundingRequestItems?.Select(i => new FundingRequestItemModel
        {
            Id             = i.Id,
            FundingRequestId = i.FundingRequestId,
            Row            = i.Row,
            ItemName       = i.ItemName,
            CategoryCode   = i.CategoryCode,
            Unit           = i.Unit,
            Quantity       = i.Quantity,
            UnitPrice      = i.UnitPrice,
            TotalPrice     = i.TotalPrice,
            ItemType       = i.ItemType,
            TargetGroups   = string.IsNullOrEmpty(i.TargetGroup)
                                ? new List<string>()
                                : i.TargetGroup.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                               .Select(s => s.Trim())
                                               .ToList(),
            ReceivedDate   = i.ReceivedDate,
            ExpiredDate    = i.ExpiredDate,
            Notes          = i.Notes
        }).ToList();

        var model = FundingRequestModel.Reconstitute(
            entity.Id,
            entity.DepotId,
            entity.RequestedBy,
            entity.TotalAmount,
            entity.Description,
            entity.AttachmentUrl,
            statusEnum,
            entity.ApprovedCampaignId,
            entity.ReviewedBy,
            entity.ReviewedAt,
            entity.RejectionReason,
            entity.CreatedAt,
            items
        );

        model.DepotName = entity.Depot?.Name;
        model.RequestedByUserName = entity.RequestedByUser?.Username;
        model.ReviewedByUserName = entity.ReviewedByUser?.Username;
        model.ApprovedCampaignName = entity.ApprovedCampaign?.Name;

        return model;
    }

    public static FundingRequest ToEntity(FundingRequestModel model)
    {
        var entity = new FundingRequest
        {
            Id = model.Id,
            DepotId = model.DepotId,
            RequestedBy = model.RequestedBy,
            TotalAmount = model.TotalAmount,
            Description = model.Description,
            AttachmentUrl = model.AttachmentUrl,
            Status = model.Status.ToString(),
            ApprovedCampaignId = model.ApprovedCampaignId,
            ReviewedBy = model.ReviewedBy,
            ReviewedAt = model.ReviewedAt,
            RejectionReason = model.RejectionReason,
            CreatedAt = model.CreatedAt
        };

        foreach (var item in model.Items)
        {
            entity.FundingRequestItems.Add(new FundingRequestItem
            {
                FundingRequestId = model.Id,
                Row            = item.Row,
                ItemName       = item.ItemName,
                CategoryCode   = item.CategoryCode,
                Unit           = item.Unit,
                Quantity       = item.Quantity,
                UnitPrice      = item.UnitPrice,
                TotalPrice     = item.TotalPrice,
                ItemType       = item.ItemType,
                TargetGroup    = string.Join(",", item.TargetGroups),
                ReceivedDate   = item.ReceivedDate,
                ExpiredDate    = item.ExpiredDate,
                Notes          = item.Notes
            });
        }

        return entity;
    }

    public static void UpdateEntity(FundingRequest entity, FundingRequestModel model)
    {
        entity.Status = model.Status.ToString();
        entity.ApprovedCampaignId = model.ApprovedCampaignId;
        entity.ReviewedBy = model.ReviewedBy;
        entity.ReviewedAt = model.ReviewedAt;
        entity.RejectionReason = model.RejectionReason;
    }
}
