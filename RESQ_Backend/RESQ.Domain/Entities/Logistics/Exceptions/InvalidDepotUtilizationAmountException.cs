using RESQ.Domain.Entities.Exceptions;

namespace RESQ.Domain.Entities.Logistics.Exceptions;

public sealed class InvalidDepotUtilizationAmountException : DomainException
{
    public InvalidDepotUtilizationAmountException(int amount) : base($"S? lu?ng s? d?ng không h?p l?: {amount}. S? lu?ng ph?i l?n hon 0.") { }

    public InvalidDepotUtilizationAmountException(decimal amount, string label) 
        : base($"S? lu?ng s? d?ng ({label}) không h?p l?: {amount}. S? lu?ng ph?i l?n hon 0.") { }
}
