namespace RESQ.Domain.Entities.Exceptions.Logistics;

/// <summary>
/// Ném ra khi tạo yêu cầu tiếp tế vi phạm quy tắc nghiệp vụ
/// (ví dụ: yêu cầu từ chính kho của mình, kho nguồn không tồn tại...).
/// </summary>
public class InvalidSupplyRequestException : DomainException
{
    public InvalidSupplyRequestException(string message) : base(message) { }
}
