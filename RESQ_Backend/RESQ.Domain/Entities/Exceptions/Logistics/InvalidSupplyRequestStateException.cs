namespace RESQ.Domain.Entities.Exceptions.Logistics;

/// <summary>
/// Ném ra khi một hành động yêu cầu tiếp tế vi phạm luồng trạng thái nghiệp vụ
/// (ví dụ: cố Accept khi đã ở trạng thái Accepted, hoặc Ship khi chưa Prepare).
/// </summary>
public class InvalidSupplyRequestStateException : DomainException
{
    public InvalidSupplyRequestStateException(string message) : base(message) { }
}
