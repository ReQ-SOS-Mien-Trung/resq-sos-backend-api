namespace RESQ.Domain.Entities.Finance;

public class ContributorDebtModel
{
    public string ContributorName { get; set; } = string.Empty;
    public string ContributorPhoneNumber { get; set; } = string.Empty;
    public decimal TotalAdvancedAmount { get; set; }
    public decimal TotalRepaidAmount { get; set; }
}

public class ContributorDebtByFundModel : ContributorDebtModel
{
    public int DepotFundId { get; set; }
}
