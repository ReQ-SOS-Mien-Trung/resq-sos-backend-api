using RESQ.Domain.Entities.Exceptions;

namespace RESQ.Domain.Entities.Finance.Exceptions;

public class InvalidCampaignStatusException : DomainException
{
    public InvalidCampaignStatusException(string message) : base(message)
    {
    }

    public InvalidCampaignStatusException(int campaignId, string status, string action)
        : base($"Chiến dịch #{campaignId} đang ở trạng thái '{status}', không thể thực hiện hành động: {action}.")
    {
    }
}
