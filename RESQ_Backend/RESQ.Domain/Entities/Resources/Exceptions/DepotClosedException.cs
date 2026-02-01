using RESQ.Domain.Entities.Exceptions;

namespace RESQ.Domain.Entities.Resources.Exceptions;

public sealed class DepotClosedException : DomainException
{
    public DepotClosedException() : base("Kho đã đóng và không thể cập nhật.") { }
}
