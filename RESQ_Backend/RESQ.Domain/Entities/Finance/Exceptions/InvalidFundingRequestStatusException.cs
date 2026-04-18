using RESQ.Domain.Entities.Exceptions;

namespace RESQ.Domain.Entities.Finance.Exceptions;

/// <summary>
/// Thrown when attempting to approve/reject a FundingRequest that is not in Pending status.
/// </summary>
public class InvalidFundingRequestStatusException : DomainException
{
    public InvalidFundingRequestStatusException(string currentStatus, string action)
        : base($"Không thể {action} yêu cầu cấp quỹ khi trạng thái hiện tại là '{currentStatus}'.")
    {
    }
}
