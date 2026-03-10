using RESQ.Domain.Entities.Exceptions;

namespace RESQ.Domain.Entities.Logistics.Exceptions;

public sealed class InvalidInventoryQuantityException : DomainException
{
    public InvalidInventoryQuantityException() 
        : base("Số lượng tồn kho không được là số âm.") 
    { 
    }
}