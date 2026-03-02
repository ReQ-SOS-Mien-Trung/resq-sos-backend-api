using RESQ.Domain.Entities.Finance;
using RESQ.Infrastructure.Entities.Finance;

namespace RESQ.Infrastructure.Mappers.Finance;

public static class DepotFundAllocationMapper
{
    public static DepotFundAllocationModel ToModel(DepotFundAllocation entity)
    {
        return new DepotFundAllocationModel
        {
            Id = entity.Id,
            FundCampaignId = entity.FundCampaignId,
            DepotId = entity.DepotId,
            Amount = entity.Amount,
            Purpose = entity.Purpose,
            Status = entity.Status,
            AllocatedBy = entity.AllocatedBy,
            AllocatedAt = entity.AllocatedAt,
            FundCampaignName = entity.FundCampaign?.Name,
            DepotName = entity.Depot?.Name
        };
    }

    public static DepotFundAllocation ToEntity(DepotFundAllocationModel model)
    {
        return new DepotFundAllocation
        {
            Id = model.Id,
            FundCampaignId = model.FundCampaignId,
            DepotId = model.DepotId,
            Amount = model.Amount,
            Purpose = model.Purpose,
            Status = model.Status,
            AllocatedBy = model.AllocatedBy,
            AllocatedAt = model.AllocatedAt
        };
    }
}
