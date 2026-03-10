using RESQ.Domain.Entities.Logistics.ValueObjects;

namespace RESQ.Application.Services.Logistics;

public class InventoryQueryService : IInventoryQueryService
{
    public InventoryAvailability ComputeAvailability(int? quantity, int? reservedQuantity)
    {
        return new InventoryAvailability(quantity ?? 0, reservedQuantity ?? 0);
    }
}
