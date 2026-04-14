using RESQ.Domain.Entities.Exceptions;

namespace RESQ.Domain.Entities.Finance.Exceptions;

public class InvalidCampaignDateException : DomainException
{
    public InvalidCampaignDateException(string message) : base(message) { }
}
