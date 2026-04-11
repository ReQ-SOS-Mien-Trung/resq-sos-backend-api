using RESQ.Domain.Entities.Exceptions;

namespace RESQ.Domain.Entities.Finance.Exceptions;

public class InvalidAdvanceLimitException : DomainException
{
    public InvalidAdvanceLimitException(decimal newLimit, decimal outstandingAmount)
        : base($"Hạn mức ứng trước mới ({newLimit:N0} VNĐ) không được thấp hơn số tiền ứng trước chưa hoàn trả ({outstandingAmount:N0} VNĐ).")
    {
    }
}
