using RESQ.Domain.Entities.Logistics.Exceptions;

namespace RESQ.Domain.Entities.Logistics.ValueObjects;

public record InventoryAvailability
{
    public int Quantity { get; }
    public int ReservedQuantity { get; }
    public int AvailableQuantity { get; }

    public InventoryAvailability(int quantity, int reservedQuantity)
    {
        if (quantity < 0)
            throw new InvalidInventoryQuantityException();
            
        if (reservedQuantity < 0)
            throw new InvalidReservedQuantityException();
            
        if (reservedQuantity > quantity)
            throw new ReservedQuantityExceedsTotalException();

        Quantity = quantity;
        ReservedQuantity = reservedQuantity;
        AvailableQuantity = quantity - reservedQuantity;
    }
}
