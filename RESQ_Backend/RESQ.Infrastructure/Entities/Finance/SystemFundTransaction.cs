using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RESQ.Infrastructure.Entities.Finance;

[Table("system_fund_transactions")]
public class SystemFundTransaction
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("system_fund_id")]
    public int SystemFundId { get; set; }

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

    [ForeignKey("SystemFundId")]
    public virtual SystemFund SystemFund { get; set; } = null!;
}
