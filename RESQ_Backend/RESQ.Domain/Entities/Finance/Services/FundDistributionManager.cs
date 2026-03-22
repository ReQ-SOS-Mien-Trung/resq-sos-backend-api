using RESQ.Domain.Entities.Finance;
using RESQ.Domain.Entities.Finance.Exceptions;
using RESQ.Domain.Enum.Finance;

namespace RESQ.Domain.Entities.Finance.Services;

public class FundDistributionManager : IFundDistributionManager
{
    public void ValidateAllocation(FundCampaignModel campaign, decimal currentBalance, decimal allocationAmount)
    {
        if (campaign == null) throw new ArgumentNullException(nameof(campaign));

        // 1. Status Check — chiến dịch Active hoặc Closed mới được điều phối quỹ cho kho
        if (campaign.Status != FundCampaignStatus.Active && campaign.Status != FundCampaignStatus.Closed)
        {
            throw new InvalidCampaignStatusException(
                campaign.Id,
                campaign.Status.ToString(),
                "Phân bổ quỹ (chỉ được thực hiện khi chiến dịch đang hoạt động hoặc đã kết thúc)"
            );
        }
        
        // 2. Soft Delete Check
        if (campaign.IsDeleted)
        {
             throw new CampaignDeletedException();
        }

        // 3. Amount Logic
        if (allocationAmount <= 0)
        {
            throw new NegativeMoneyException(allocationAmount);
        }

        // 4. Balance Check
        if (currentBalance < allocationAmount)
        {
            throw new InsufficientCampaignFundsException(currentBalance, allocationAmount);
        }
    }
}
