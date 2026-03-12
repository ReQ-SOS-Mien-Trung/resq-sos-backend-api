using RESQ.Domain.Entities.Logistics.ValueObjects;

namespace RESQ.Domain.Entities.Logistics.Services;

public interface IInventoryQueryService
{
    InventoryAvailability ComputeAvailability(int? quantity, int? reservedQuantity);
}