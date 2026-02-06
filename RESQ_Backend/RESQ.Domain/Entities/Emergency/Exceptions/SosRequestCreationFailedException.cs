using RESQ.Domain.Entities.Exceptions;

namespace RESQ.Domain.Entities.Emergency.Exceptions;

public sealed class SosRequestCreationFailedException : DomainException
{
    public SosRequestCreationFailedException()
        : base("Không thể tạo yêu cầu SOS. Vui lòng thử lại sau.") { }
}
