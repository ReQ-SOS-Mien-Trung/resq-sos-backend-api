using RESQ.Domain.Entities.Exceptions;

namespace RESQ.Domain.Entities.Logistics.Exceptions;

/// <summary>
/// Ném ra khi cố thực hiện thao tác trên kho đang trong quá trình đóng (Closing).
/// Maps sang HTTP 409 Conflict qua DomainExceptionBehaviour.
/// </summary>
public sealed class DepotClosingException : DomainException
{
    public DepotClosingException()
        : base("Kho đang trong quá trình đóng, không thể thực hiện thao tác này. Vui lòng hoàn tất hoặc huỷ yêu cầu đóng kho trước.")
    { }

    public DepotClosingException(string message) : base(message) { }
}
