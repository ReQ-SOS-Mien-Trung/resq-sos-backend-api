using RESQ.Domain.Entities.Exceptions;

namespace RESQ.Domain.Entities.Resources.Exceptions;

public sealed class InvalidDepotUtilizationAmountException : DomainException
{
    public InvalidDepotUtilizationAmountException(int amount) : base($"Invalid utilization amount: {amount}. Amount must be greater than zero.") { }
}
