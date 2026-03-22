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

    /// <summary>Hạn mức tối đa mà kho được phép tự ứng (balance âm). 0 = không cho âm. Admin cấu hình.</summary>
    [Column("max_advance_limit")]
    public decimal MaxAdvanceLimit { get; set; }

    [Column("last_updated_at", TypeName = "timestamp with time zone")]
    public DateTime LastUpdatedAt { get; set; }

    [ForeignKey("DepotId")]
    [InverseProperty("DepotFund")]
    public virtual Depot Depot { get; set; } = null!;

    [InverseProperty("DepotFund")]
    public virtual ICollection<DepotFundTransaction> DepotFundTransactions { get; set; } = new List<DepotFundTransaction>();
}
