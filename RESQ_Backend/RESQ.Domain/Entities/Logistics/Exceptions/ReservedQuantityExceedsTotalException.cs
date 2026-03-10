using RESQ.Domain.Entities.Exceptions;

namespace RESQ.Domain.Entities.Logistics.Exceptions;

public sealed class ReservedQuantityExceedsTotalException : DomainException
{
    public ReservedQuantityExceedsTotalException() 
        : base("Số lượng đặt trước không được vượt quá tổng số lượng tồn kho.") 
    { 
    }
}