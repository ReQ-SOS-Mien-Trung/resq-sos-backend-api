using RESQ.Domain.Entities.Exceptions;

namespace RESQ.Domain.Entities.Finance.Exceptions;

public class AdvanceLimitExceededException : DomainException
{
    public AdvanceLimitExceededException(decimal currentBalance, decimal requestedAmount, decimal maxAdvanceLimit)
        : base($"Quỹ kho không đủ và vượt hạn mức tự ứng. " +
               $"Số dư: {currentBalance:N0} VNĐ, chi phí: {requestedAmount:N0} VNĐ, " +
               $"hạn mức tự ứng tối đa: {maxAdvanceLimit:N0} VNĐ.")
    {
    }
}
