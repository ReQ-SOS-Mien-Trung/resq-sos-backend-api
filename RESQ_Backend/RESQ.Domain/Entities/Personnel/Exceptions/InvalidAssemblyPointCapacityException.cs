using RESQ.Domain.Entities.Exceptions;

namespace RESQ.Domain.Entities.Personnel.Exceptions;

public class InvalidAssemblyPointCapacityException : DomainException
{
    public InvalidAssemblyPointCapacityException(int capacity) 
        : base($"Sức chứa đội cứu hộ không hợp lệ: {capacity}. Sức chứa phải lớn hơn 0.") 
    { 
    }
}