using RESQ.Domain.Entities.Exceptions;

namespace RESQ.Domain.Entities.Resources.Exceptions;

public sealed class InvalidDepotStatusException : DomainException
{
    public InvalidDepotStatusException(string status) : base($"Trạng thái kho không hợp lệ: {status}") {}
}
