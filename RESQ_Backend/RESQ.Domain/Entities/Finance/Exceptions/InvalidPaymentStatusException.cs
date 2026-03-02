using RESQ.Domain.Entities.Exceptions;

namespace RESQ.Domain.Entities.Finance.Exceptions;

public class InvalidPaymentStatusException : DomainException
{
    public InvalidPaymentStatusException(string currentStatus, string newStatus)
        : base($"Không thể chuyển trạng thái thanh toán từ '{currentStatus}' sang '{newStatus}'.")
    {
    }
}
