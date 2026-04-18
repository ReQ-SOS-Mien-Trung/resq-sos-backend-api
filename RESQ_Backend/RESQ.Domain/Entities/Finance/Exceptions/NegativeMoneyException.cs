using RESQ.Domain.Entities.Exceptions;

namespace RESQ.Domain.Entities.Finance.Exceptions;

public class NegativeMoneyException : DomainException
{
    public NegativeMoneyException(decimal amount)
        : base($"Số tiền phải lớn hơn 0. Giá trị nhận được: {amount}.")
    {
    }
}
