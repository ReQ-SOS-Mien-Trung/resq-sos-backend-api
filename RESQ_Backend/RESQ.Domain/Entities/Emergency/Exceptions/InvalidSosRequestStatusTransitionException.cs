using RESQ.Domain.Entities.Exceptions;
using RESQ.Domain.Enum.Emergency;

namespace RESQ.Domain.Entities.Emergency.Exceptions;

public sealed class InvalidSosRequestStatusTransitionException : DomainException
{
    public InvalidSosRequestStatusTransitionException(SosRequestStatus currentStatus, SosRequestStatus newStatus)
        : base($"Không thể chuyển trạng thái từ '{currentStatus}' sang '{newStatus}'.") { }
}
