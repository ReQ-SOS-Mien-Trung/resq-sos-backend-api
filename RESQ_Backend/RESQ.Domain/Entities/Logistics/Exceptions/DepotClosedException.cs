using RESQ.Domain.Entities.Exceptions;

namespace RESQ.Domain.Entities.Logistics.Exceptions;

// Inherits DomainException -> Triggers HTTP 400
public sealed class DepotClosedException : DomainException
{
    public DepotClosedException() : base("Kho đã đóng cửa và không thể thực hiện cập nhật.") { }
}
