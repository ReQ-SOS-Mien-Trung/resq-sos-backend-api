using RESQ.Domain.Entities.Logistics;
using RESQ.Infrastructure.Entities.Logistics;

namespace RESQ.Infrastructure.Mappers.Logistics;

public static class OrganizationReliefItemMapper
{
    public static OrganizationReliefItem ToEntity(OrganizationReliefItemModel model)
    {
        return new OrganizationReliefItem
        {
            Id = model.Id,
            OrganizationId = model.OrganizationId,
            ReliefItemId = model.ReliefItemId,
            Quantity = model.Quantity,
            ReceivedDate = model.ReceivedDate,
            ExpiredDate = model.ExpiredDate,
            Notes = model.Notes,
            ReceivedBy = model.ReceivedBy,
            ReceivedAt = model.ReceivedAt,
            CreatedAt = model.CreatedAt
        };
    }
}