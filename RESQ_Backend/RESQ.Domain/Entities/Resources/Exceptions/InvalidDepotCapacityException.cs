using RESQ.Domain.Entities.Exceptions;

namespace RESQ.Domain.Entities.Resources.Exceptions;

public class InvalidDepotCapacityException : DomainException
{
    public InvalidDepotCapacityException(int capacity) : base($"Sức chứa kho không hợp lệ: {capacity}. Sức chứa phải lớn hơn 0.") { }
}
