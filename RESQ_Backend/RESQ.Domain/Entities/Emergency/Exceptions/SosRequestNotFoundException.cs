using RESQ.Domain.Entities.Exceptions;

namespace RESQ.Domain.Entities.Emergency.Exceptions;

public sealed class SosRequestNotFoundException : DomainException
{
    public SosRequestNotFoundException(int id)
        : base($"Không tìm thấy yêu cầu SOS với Id: {id}.") { }

    public SosRequestNotFoundException(Guid userId)
        : base($"Không tìm thấy yêu cầu SOS cho người dùng: {userId}.") { }
}
