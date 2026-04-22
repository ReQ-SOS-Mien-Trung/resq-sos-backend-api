namespace RESQ.Domain.Entities.Logistics.Models;

public class DepotClosingBlockersModel
{
    public int ReservedConsumableItemCount { get; set; }
    public int ReservedConsumableUnitCount { get; set; }
    public int NonAvailableReusableItemModelCount { get; set; }
    public int NonAvailableReusableUnitCount { get; set; }

    public bool HasBlockingReservedConsumables => ReservedConsumableUnitCount > 0;
    public bool HasBlockingReusableStates => NonAvailableReusableUnitCount > 0;
    public bool HasAnyBlockingItems => HasBlockingReservedConsumables || HasBlockingReusableStates;
}
