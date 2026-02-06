using RESQ.Domain.Entities.Exceptions;

namespace RESQ.Domain.Entities.Emergency.Exceptions;

public sealed class InvalidSosRequestStatusTransitionException : DomainException
{
    public InvalidSosRequestStatusTransitionException(string currentStatus, string newStatus)
        : base($"Không thể chuyển trạng thái từ '{currentStatus}' sang '{newStatus}'.") { }
}
