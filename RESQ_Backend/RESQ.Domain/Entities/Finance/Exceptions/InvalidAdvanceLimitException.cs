using RESQ.Domain.Entities.Exceptions;

namespace RESQ.Domain.Entities.Finance.Exceptions;

public class InvalidAdvanceLimitException : DomainException
{
    public InvalidAdvanceLimitException(decimal newLimit, decimal outstandingAmount)
        : base(
            $"Hạn mức ứng trước mới ({newLimit:N0} VND) không được nhỏ hơn tổng nợ đang ứng ({outstandingAmount:N0} VND).")
    {
    }
}
