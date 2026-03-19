using RESQ.Domain.Entities.Finance;
using RESQ.Domain.Enum.Finance;
using RESQ.Infrastructure.Entities.Finance;

namespace RESQ.Infrastructure.Mappers.Finance;

public static class DepotFundTransactionMapper
{
    public static DepotFundTransactionModel ToModel(DepotFundTransaction entity)
    {
        var typeEnum = DepotFundTransactionType.Allocation;
        if (!string.IsNullOrEmpty(entity.TransactionType))
        {
            Enum.TryParse(entity.TransactionType, true, out typeEnum);
        }

        return new DepotFundTransactionModel
        {
            Id = entity.Id,
            DepotFundId = entity.DepotFundId,
            TransactionType = typeEnum,
            Amount = entity.Amount,
            ReferenceType = entity.ReferenceType,
            ReferenceId = entity.ReferenceId,
            Note = entity.Note,
            CreatedBy = entity.CreatedBy,
            CreatedAt = entity.CreatedAt
        };
    }

    public static DepotFundTransaction ToEntity(DepotFundTransactionModel model)
    {
        return new DepotFundTransaction
        {
            Id = model.Id,
            DepotFundId = model.DepotFundId,
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
