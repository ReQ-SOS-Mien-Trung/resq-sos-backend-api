namespace RESQ.Application.UseCases.Finance.Queries.GetDepotAdvancers;

public class DepotAdvancerDto
{
    public string ContributorName { get; set; } = null!;
    public string ContributorPhoneNumber { get; set; } = null!;
    public decimal TotalAdvancedAmount { get; set; }
    public decimal TotalRepaidAmount { get; set; }
    public decimal OutstandingAmount { get; set; }
    public decimal RepaidPercentage { get; set; }
}
