using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RESQ.Infrastructure.Entities.Finance;

[Table("depot_fund_transactions")]
public partial class DepotFundTransaction
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("depot_fund_id")]
    public int DepotFundId { get; set; }

    [Column("transaction_type")]
    [StringLength(50)]
    public string TransactionType { get; set; } = string.Empty;

    [Column("amount")]
    public decimal Amount { get; set; }

    [Column("reference_type")]
    [StringLength(100)]
    public string? ReferenceType { get; set; }

    [Column("reference_id")]
    public int? ReferenceId { get; set; }

    [Column("note")]
    [StringLength(500)]
    public string? Note { get; set; }

    [Column("created_by")]
    public Guid? CreatedBy { get; set; }

    [Column("created_at", TypeName = "timestamp with time zone")]
    public DateTime CreatedAt { get; set; }

    [Column("contributor_name")]
    [StringLength(255)]
    public string? ContributorName { get; set; }

    [Column("contributor_id")]
    public Guid? ContributorId { get; set; }

    [ForeignKey("DepotFundId")]
    [InverseProperty("DepotFundTransactions")]
    public virtual DepotFund DepotFund { get; set; } = null!;
}
