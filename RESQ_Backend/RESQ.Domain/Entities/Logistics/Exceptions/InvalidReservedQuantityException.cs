using RESQ.Domain.Entities.Exceptions;

namespace RESQ.Domain.Entities.Logistics.Exceptions;

public sealed class InvalidReservedQuantityException : DomainException
{
    public InvalidReservedQuantityException() 
        : base("Số lượng đặt trước không được là số âm.") 
    { 
    }
}