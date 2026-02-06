using RESQ.Domain.Entities.Exceptions;

namespace RESQ.Domain.Entities.Emergency.Exceptions;

public sealed class InvalidSosRequestUserException : DomainException
{
    public InvalidSosRequestUserException()
        : base("UserId không hợp lệ. UserId không được để trống.") { }
}
