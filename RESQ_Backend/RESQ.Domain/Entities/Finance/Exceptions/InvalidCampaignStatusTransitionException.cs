using RESQ.Domain.Entities.Exceptions;

namespace RESQ.Domain.Entities.Finance.Exceptions;

public class InvalidCampaignStatusTransitionException : DomainException
{
    public InvalidCampaignStatusTransitionException(string currentStatus, string newStatus)
        : base($"Không thể chuyển trạng thái chiến dịch từ '{currentStatus}' sang '{newStatus}'.")
    {
    }
}
