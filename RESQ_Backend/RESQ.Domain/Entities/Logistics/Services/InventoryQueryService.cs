using RESQ.Domain.Entities.Logistics.ValueObjects;

namespace RESQ.Domain.Entities.Logistics.Services;

public class InventoryQueryService : IInventoryQueryService
{
    /// <inheritdoc />
    public InventoryAvailability ComputeAvailability(int? quantity, int? missionReserved, int? transferReserved)
    {
        return new InventoryAvailability(
            quantity        ?? 0,
            missionReserved ?? 0,
            transferReserved ?? 0);
    }
}
