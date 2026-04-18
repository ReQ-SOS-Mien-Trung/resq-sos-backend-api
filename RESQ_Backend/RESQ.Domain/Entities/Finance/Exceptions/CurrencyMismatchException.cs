using RESQ.Domain.Entities.Exceptions;

namespace RESQ.Domain.Entities.Finance.Exceptions;

public class CurrencyMismatchException : DomainException
{
    public CurrencyMismatchException(string currencyA, string currencyB) 
        : base($"Không thể thực hiện phép toán trên các loại tiền tệ khác nhau ({currencyA} và {currencyB}).")
    {
    }
}
