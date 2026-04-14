using RESQ.Domain.Entities.Exceptions;

namespace RESQ.Domain.Entities.Logistics.Exceptions;

public class InvalidDepotCapacityException : DomainException
{
    public InvalidDepotCapacityException(int capacity) : base($"S?c ch?a kho không h?p l?: {capacity}. S?c ch?a ph?i l?n hon 0.") { }

    public InvalidDepotCapacityException(decimal capacity, string label) 
        : base($"S?c ch?a kho ({label}) không h?p l?: {capacity}. S?c ch?a ph?i l?n hon 0.") { }
}
