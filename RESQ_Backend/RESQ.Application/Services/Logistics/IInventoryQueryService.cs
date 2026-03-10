using RESQ.Domain.Entities.Logistics.ValueObjects;

namespace RESQ.Application.Services.Logistics;

public interface IInventoryQueryService
{
    InventoryAvailability ComputeAvailability(int? quantity, int? reservedQuantity);
}
