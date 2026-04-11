using RESQ.Domain.Entities.Exceptions;

namespace RESQ.Domain.Entities.Finance.Exceptions;

public class InsufficientDepotFundException : DomainException
{
    public InsufficientDepotFundException(decimal currentBalance, decimal requestedAmount)
        : base(
            $"Số dư quỹ kho ({currentBalance:N0} VND) không đủ để thực hiện giao dịch ({requestedAmount:N0} VND).")
    {
    }
}
