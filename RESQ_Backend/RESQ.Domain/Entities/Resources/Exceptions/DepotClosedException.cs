using RESQ.Domain.Entities.Exceptions;

namespace RESQ.Domain.Entities.Resources.Exceptions;

public sealed class DepotClosedException : DomainException
{
    public DepotClosedException() : base("Depot is closed and cannot be updated.") { }
}
