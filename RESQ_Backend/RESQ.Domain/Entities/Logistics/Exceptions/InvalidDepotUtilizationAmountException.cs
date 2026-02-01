using RESQ.Domain.Entities.Exceptions;

namespace RESQ.Domain.Entities.Logistics.Exceptions;

public sealed class InvalidDepotUtilizationAmountException : DomainException
{
    public InvalidDepotUtilizationAmountException(int amount) : base($"Số lượng sử dụng không hợp lệ: {amount}. Số lượng phải lớn hơn 0.") { }
}
