using RESQ.Domain.Entities.Exceptions;

namespace RESQ.Domain.Entities.Finance.Exceptions;

public class InvalidCampaignTargetAmountException : DomainException
{
    public InvalidCampaignTargetAmountException(string message) : base(message) { }
}
