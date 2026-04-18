using RESQ.Domain.Entities.Exceptions;

namespace RESQ.Domain.Entities.Logistics.Exceptions;

// Inherits DomainException -> Triggers HTTP 400
public sealed class DepotNameDuplicatedException : DomainException
{
    public DepotNameDuplicatedException(string name) 
        : base($"Kho với tên '{name}' đã tồn tại.") { }
}
