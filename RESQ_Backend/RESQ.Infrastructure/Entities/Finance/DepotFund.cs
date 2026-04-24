using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RESQ.Infrastructure.Entities.Logistics;

namespace RESQ.Infrastructure.Entities.Finance;

[Table("depot_funds")]
public partial class DepotFund
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("depot_id")]
    public int DepotId { get; set; }

    [Column("balance")]
    public decimal Balance { get; set; }

    [Column("last_updated_at", TypeName = "timestamp with time zone")]
    public DateTime LastUpdatedAt { get; set; }

    [Column("fund_source_type")]
    [StringLength(50)]
    public string? FundSourceType { get; set; }

    [Column("fund_source_id")]
    public int? FundSourceId { get; set; }

    public uint RowVersion { get; set; }

    [ForeignKey(nameof(DepotId))]
    [InverseProperty(nameof(Depot.DepotFunds))]
    public virtual Depot Depot { get; set; } = null!;

    [InverseProperty(nameof(DepotFundTransaction.DepotFund))]
    public virtual ICollection<DepotFundTransaction> DepotFundTransactions { get; set; } = new List<DepotFundTransaction>();
}
