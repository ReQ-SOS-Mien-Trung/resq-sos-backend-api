using RESQ.Domain.Entities.Exceptions;

namespace RESQ.Domain.Entities.Personnel.Exceptions;

public sealed class InvalidAssemblyEventStatusException : DomainException
{
    public InvalidAssemblyEventStatusException(string message) : base(message) { }
}
