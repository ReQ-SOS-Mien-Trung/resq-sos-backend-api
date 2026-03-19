using RESQ.Domain.Entities.Finance;
using RESQ.Infrastructure.Entities.Finance;

namespace RESQ.Infrastructure.Mappers.Finance;

public static class DepotFundMapper
{
    public static DepotFundModel ToModel(DepotFund entity)
    {
        var model = DepotFundModel.Reconstitute(
            entity.Id,
            entity.DepotId,
            entity.Balance,
            entity.LastUpdatedAt
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
            LastUpdatedAt = model.LastUpdatedAt
        };
    }

    public static void UpdateEntity(DepotFund entity, DepotFundModel model)
    {
        entity.Balance = model.Balance;
        entity.LastUpdatedAt = model.LastUpdatedAt;
    }
}
