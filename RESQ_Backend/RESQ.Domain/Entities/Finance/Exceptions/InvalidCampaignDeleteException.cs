using RESQ.Domain.Entities.Exceptions;

namespace RESQ.Domain.Entities.Finance.Exceptions;

public class InvalidCampaignDeleteException : DomainException
{
    public InvalidCampaignDeleteException(string reason) 
        : base($"Không thể xóa chiến dịch: {reason}")
    {
    }
}
