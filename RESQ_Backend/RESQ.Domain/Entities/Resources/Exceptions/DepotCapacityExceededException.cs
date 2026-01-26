using RESQ.Domain.Entities.Exceptions;

namespace RESQ.Domain.Entities.Resources.Exceptions;

public sealed class DepotCapacityExceededException : DomainException
{
    public DepotCapacityExceededException() : base("Depot capacity exceeded") { }
}
