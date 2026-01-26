using RESQ.Domain.Entities.Exceptions;

namespace RESQ.Domain.Entities.Resources.Exceptions;

public sealed class InvalidDepotStatusException : DomainException
{
    public InvalidDepotStatusException(string status) : base($"Invalid depot status value: {status}") {}
}
