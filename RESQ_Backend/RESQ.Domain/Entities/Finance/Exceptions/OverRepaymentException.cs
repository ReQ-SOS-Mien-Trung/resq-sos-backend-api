using RESQ.Domain.Entities.Exceptions;

namespace RESQ.Domain.Entities.Finance.Exceptions;

public class OverRepaymentException : DomainException
{
    public OverRepaymentException(decimal requestedAmount, decimal outstandingAmount)
        : base(
            $"Số tiền hoàn trả ({requestedAmount:N0} VND) vượt quá số nợ còn lại ({outstandingAmount:N0} VND).")
    {
    }
}
