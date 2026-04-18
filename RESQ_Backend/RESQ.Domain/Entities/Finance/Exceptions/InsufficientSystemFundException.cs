using RESQ.Domain.Entities.Exceptions;

namespace RESQ.Domain.Entities.Finance.Exceptions;

public class InsufficientSystemFundException : DomainException
{
    public InsufficientSystemFundException(decimal available, decimal requested)
        : base($"Quỹ hệ thống không đủ số dư. Hiện có: {available:N0} VNĐ, Yêu cầu: {requested:N0} VNĐ.")
    {
    }
}
