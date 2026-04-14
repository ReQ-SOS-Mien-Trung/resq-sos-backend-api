using RESQ.Domain.Entities.Exceptions;

namespace RESQ.Domain.Entities.Logistics.Exceptions;

public sealed class InvalidDepotStatusException : DomainException
{
    public InvalidDepotStatusException(string status) : base($"Tr?ng thái kho không h?p l?: {status}") {}
}
