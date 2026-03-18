namespace RESQ.Domain.Entities.Exceptions.Logistics;

/// <summary>
/// Ném ra khi người dùng cố thực hiện hành động trên một yêu cầu tiếp tế
/// mà họ không phải là manager có thẩm quyền (kho nguồn hoặc kho yêu cầu).
/// </summary>
public class SupplyRequestAccessDeniedException : DomainException
{
    public SupplyRequestAccessDeniedException(string message) : base(message) { }
}
