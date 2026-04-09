using RESQ.Domain.Entities.Finance;
using RESQ.Domain.Enum.Finance;
using RESQ.Infrastructure.Entities.Finance;

namespace RESQ.Infrastructure.Mappers.Finance;

public static class DepotFundMapper
{
    public static DepotFundModel ToModel(DepotFund entity)
    {
        FundSourceType? sourceType = entity.FundSourceType switch
        {
            "Campaign" => Domain.Enum.Finance.FundSourceType.Campaign,
            "SystemFund" => Domain.Enum.Finance.FundSourceType.SystemFund,
            _ => null
        };

        var model = DepotFundModel.Reconstitute(
            entity.Id,
            entity.DepotId,
            entity.Balance,
            entity.MaxAdvanceLimit,
            entity.LastUpdatedAt,
            sourceType,
            entity.FundSourceId
        );

        model.DepotName = entity.Depot?.Name;

        return model;
    }

    public static DepotFund ToEntity(DepotFundModel model)
    {
        return new DepotFund
        {
            Id = model.Id,
            DepotId = model.DepotId,
            Balance = model.Balance,
            MaxAdvanceLimit = model.MaxAdvanceLimit,
            LastUpdatedAt = model.LastUpdatedAt,
            FundSourceType = model.FundSourceType?.ToString(),
            FundSourceId = model.FundSourceId
        };
    }

    public static void UpdateEntity(DepotFund entity, DepotFundModel model)
    {
        entity.Balance = model.Balance;
        entity.MaxAdvanceLimit = model.MaxAdvanceLimit;
        entity.LastUpdatedAt = model.LastUpdatedAt;
        entity.FundSourceType = model.FundSourceType?.ToString();
        entity.FundSourceId = model.FundSourceId;
    }
}
