using RESQ.Domain.Entities.Exceptions;

namespace RESQ.Domain.Entities.Emergency.Exceptions;

public sealed class InvalidSosRequestMessageException : DomainException
{
    public InvalidSosRequestMessageException()
        : base("Tin nhắn SOS không hợp lệ. Tin nhắn không được để trống.") { }
}
