using RESQ.Domain.Entities.Exceptions;

namespace RESQ.Domain.Entities.Finance.Exceptions;

public class NegativeMoneyException : DomainException
{
    public NegativeMoneyException(decimal amount) 
        : base($"Số tiền không được âm. Giá trị nhận được: {amount}")
    {
    }
}