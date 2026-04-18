using RESQ.Domain.Entities.Exceptions;

namespace RESQ.Domain.Entities.Logistics.Exceptions;

public sealed class InvalidReliefItemQuantityException : DomainException
{
    public InvalidReliefItemQuantityException(int quantity) 
        : base($"Số lượng vật phẩm không hợp lệ: {quantity}. Số lượng phải lớn hơn 0.") { }
}
