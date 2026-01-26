using RESQ.Domain.Entities.Exceptions;

namespace RESQ.Domain.Entities.Resources.Exceptions;

public class InvalidDepotCapacityException : DomainException
{
    public InvalidDepotCapacityException(int capacity) : base($"Invalid depot capacity: {capacity}. Capacity must be greater than zero.") { }
}
