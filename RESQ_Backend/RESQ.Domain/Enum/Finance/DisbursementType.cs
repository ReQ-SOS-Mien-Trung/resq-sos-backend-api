namespace RESQ.Domain.Enum.Finance;

/// <summary>
/// Kiểu giải ngân từ campaign.
/// </summary>
public enum DisbursementType
{
    /// <summary>Admin chủ động cấp tiền cho depot (Cách 1).</summary>
    AdminAllocation,
    
    /// <summary>Admin duyệt FundingRequest từ depot (Cách 2).</summary>
    FundingRequestApproval
}
