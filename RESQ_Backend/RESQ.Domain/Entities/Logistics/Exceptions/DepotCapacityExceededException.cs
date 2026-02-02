using RESQ.Domain.Entities.Exceptions;

namespace RESQ.Domain.Entities.Logistics.Exceptions;

// Inherits DomainException -> Triggers HTTP 400
public sealed class DepotCapacityExceededException : DomainException
{
    public DepotCapacityExceededException() 
        : base("Sức chứa kho không đủ hoặc thấp hơn số lượng hàng hiện tại.") { }
}
