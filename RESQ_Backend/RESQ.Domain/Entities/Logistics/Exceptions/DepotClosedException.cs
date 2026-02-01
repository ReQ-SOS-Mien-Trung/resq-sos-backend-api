using RESQ.Domain.Entities.Exceptions;

namespace RESQ.Domain.Entities.Logistics.Exceptions;

public sealed class DepotClosedException : DomainException
{
    public DepotClosedException() : base("Kho đã đóng và không thể cập nhật.") { }
}
