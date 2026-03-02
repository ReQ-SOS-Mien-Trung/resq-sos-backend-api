using RESQ.Domain.Entities.Finance.Exceptions;

namespace RESQ.Domain.Entities.Finance.ValueObjects;

public record Money
{
    public decimal Amount { get; init; }
    public string Currency { get; init; }

    public Money(decimal amount, string currency = "VND")
    {
        if (amount < 0)
        {
            throw new NegativeMoneyException(amount);
        }

        Amount = amount;
        Currency = currency;
    }

    public static Money Zero(string currency = "VND") => new(0, currency);

    public static Money Create(decimal amount, string currency = "VND")
    {
        return new Money(amount, currency);
    }

    public static Money operator +(Money a, Money b)
    {
        if (a.Currency != b.Currency)
        {
            throw new CurrencyMismatchException(a.Currency, b.Currency);
        }
        return new Money(a.Amount + b.Amount, a.Currency);
    }

    public static Money operator -(Money a, Money b)
    {
        if (a.Currency != b.Currency)
        {
            throw new CurrencyMismatchException(a.Currency, b.Currency);
        }
        return new Money(a.Amount - b.Amount, a.Currency);
    }
    
    public override string ToString() => $"{Amount:N0} {Currency}";
}
