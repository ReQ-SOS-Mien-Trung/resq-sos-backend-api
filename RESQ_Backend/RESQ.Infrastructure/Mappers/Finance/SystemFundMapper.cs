using RESQ.Domain.Entities.Finance;
using RESQ.Domain.Enum.Finance;
using RESQ.Infrastructure.Entities.Finance;

namespace RESQ.Infrastructure.Mappers.Finance;

public static class SystemFundMapper
{
    public static SystemFundModel ToModel(SystemFund entity)
    {
        return SystemFundModel.Reconstitute(
            entity.Id,
            entity.Name,
            entity.Balance,
            entity.LastUpdatedAt);
    }

    public static void UpdateEntity(SystemFund entity, SystemFundModel model)
    {
        entity.Balance = model.Balance;
        entity.LastUpdatedAt = model.LastUpdatedAt;
    }

    public static SystemFundTransactionModel ToTransactionModel(SystemFundTransaction entity)
    {
        return new SystemFundTransactionModel
        {
            Id = entity.Id,
            SystemFundId = entity.SystemFundId,
            TransactionType = Enum.TryParse<SystemFundTransactionType>(entity.TransactionType, out var t)
                ? t : SystemFundTransactionType.LiquidationRevenue,
            Amount = entity.Amount,
            ReferenceType = entity.ReferenceType,
            ReferenceId = entity.ReferenceId,
            Note = entity.Note,
            CreatedBy = entity.CreatedBy,
            CreatedAt = entity.CreatedAt
        };
    }

    public static SystemFundTransaction ToTransactionEntity(SystemFundTransactionModel model)
    {
        return new SystemFundTransaction
        {
            SystemFundId = model.SystemFundId,
            TransactionType = model.TransactionType.ToString(),
            Amount = model.Amount,
            ReferenceType = model.ReferenceType,
            ReferenceId = model.ReferenceId,
            Note = model.Note,
            CreatedBy = model.CreatedBy,
            CreatedAt = model.CreatedAt
        };
    }
}
