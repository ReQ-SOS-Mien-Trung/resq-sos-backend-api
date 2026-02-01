using RESQ.Domain.Entities.Exceptions;

namespace RESQ.Domain.Entities.Logistics.Exceptions;

public sealed class DepotCapacityExceededException : DomainException
{
    public DepotCapacityExceededException() : base("Vượt quá sức chứa kho") { }
}
