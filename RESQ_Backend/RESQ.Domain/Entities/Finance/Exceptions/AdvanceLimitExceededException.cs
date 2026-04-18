using RESQ.Domain.Entities.Exceptions;

namespace RESQ.Domain.Entities.Finance.Exceptions;

public class AdvanceLimitExceededException : DomainException
{
    public AdvanceLimitExceededException(decimal currentOutstanding, decimal requestedAmount, decimal maxAdvanceLimit)
        : base(
            $"Vượt hạn mức ứng trước. Đang nợ: {currentOutstanding:N0} VND, yêu cầu ứng: {requestedAmount:N0} VND, hạn mức: {maxAdvanceLimit:N0} VND.")
    {
    }
}
