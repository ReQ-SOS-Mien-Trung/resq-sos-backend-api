using RESQ.Domain.Entities.Exceptions;

namespace RESQ.Domain.Entities.Personnel.Exceptions;

public sealed class BadAssemblyPointUpdateException : DomainException
{
    public BadAssemblyPointUpdateException(string reason)
        : base(reason)
    {
    }
}
