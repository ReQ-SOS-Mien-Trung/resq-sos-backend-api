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

    /// <summary>Hạn mức tối đa mà kho được phép tự ứng. 0 = không cho ứng. Admin cấu hình.</summary>
    [Column("advance_limit")]
    public decimal AdvanceLimit { get; set; }

    /// <summary>Tổng tiền đã được các cá nhân ứng trước cho kho.</summary>
    [Column("outstanding_advance_amount")]
    public decimal OutstandingAdvanceAmount { get; set; }

    [Column("last_updated_at", TypeName = "timestamp with time zone")]
    public DateTime LastUpdatedAt { get; set; }

    /// <summary>Loại nguồn quỹ: "Campaign" hoặc "SystemFund". Null = legacy fund chưa gắn nguồn.</summary>
    [Column("fund_source_type")]
    [StringLength(50)]
    public string? FundSourceType { get; set; }

    /// <summary>
    /// ID nguồn quỹ:
    /// - Campaign → FundCampaignId
    /// - SystemFund → null (singleton)
    /// </summary>
    [Column("fund_source_id")]
    public int? FundSourceId { get; set; }

    [ForeignKey("DepotId")]
    [InverseProperty("DepotFunds")]
    public virtual Depot Depot { get; set; } = null!;

    [InverseProperty("DepotFund")]
    public virtual ICollection<DepotFundTransaction> DepotFundTransactions { get; set; } = new List<DepotFundTransaction>();
}
