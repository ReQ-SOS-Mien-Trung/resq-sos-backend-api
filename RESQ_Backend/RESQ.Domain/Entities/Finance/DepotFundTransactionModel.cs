using RESQ.Domain.Enum.Finance;

namespace RESQ.Domain.Entities.Finance;

/// <summary>
/// Depot fund transaction for audit trail.
/// </summary>
public class DepotFundTransactionModel
{
    public int Id { get; set; }
    public int DepotFundId { get; set; }
    public DepotFundTransactionType TransactionType { get; set; }
    public decimal Amount { get; set; }
    public string? ReferenceType { get; set; }
    public int? ReferenceId { get; set; }
    public string? Note { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? ContributorName { get; set; }
    public string? ContributorPhoneNumber { get; set; }
    public Guid? ContributorId { get; set; }
}
