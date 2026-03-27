using RESQ.Domain.Entities.Exceptions;

namespace RESQ.Domain.Entities.Personnel.Exceptions;

public class InvalidAssemblyPointCapacityException : DomainException
{
    public InvalidAssemblyPointCapacityException(int capacity) 
        : base($"Sức chứa không hợp lệ: {capacity}. Số người tối đa phải lớn hơn 0.") 
    { 
    }
}
