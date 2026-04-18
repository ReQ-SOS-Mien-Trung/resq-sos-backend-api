using RESQ.Domain.Entities.Finance;

namespace RESQ.Domain.Entities.Finance.Services;

public interface IFundDistributionManager
{
    /// <summary>
    /// Validates if a campaign can allocate the specified amount to a depot.
    /// Checks status, remaining balance, and amount validity.
    /// </summary>
    /// <param name="campaign">The source campaign.</param>
    /// <param name="currentBalance">The calculated available balance (Total Raised - Total Spent).</param>
    /// <param name="allocationAmount">The amount to allocate.</param>
    void ValidateAllocation(FundCampaignModel campaign, decimal currentBalance, decimal allocationAmount);
}
