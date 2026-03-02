using RESQ.Domain.Entities.Exceptions;

namespace RESQ.Domain.Entities.Finance.Exceptions;

public class InsufficientCampaignFundsException : DomainException
{
    public InsufficientCampaignFundsException(decimal available, decimal requested)
        : base($"Số dư quỹ không đủ để thực hiện giao dịch. Hiện có: {available:N0} VNĐ, Yêu cầu: {requested:N0} VNĐ.")
    {
    }
}
