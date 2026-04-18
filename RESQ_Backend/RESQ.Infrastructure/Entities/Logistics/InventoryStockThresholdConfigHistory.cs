using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RESQ.Infrastructure.Entities.Logistics;

[Table("inventory_stock_threshold_config_history")]
public class InventoryStockThresholdConfigHistory
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("config_id")]
    public int? ConfigId { get; set; }

    [Column("scope_type")]
    [StringLength(32)]
    public string ScopeType { get; set; } = string.Empty;

    [Column("depot_id")]
    public int? DepotId { get; set; }

    [Column("category_id")]
    public int? CategoryId { get; set; }

    [Column("item_model_id")]
    public int? ItemModelId { get; set; }

    [Column("old_danger_ratio", TypeName = "numeric(5,4)")]
    public decimal? OldDangerRatio { get; set; }

    [Column("old_warning_ratio", TypeName = "numeric(5,4)")]
    public decimal? OldWarningRatio { get; set; }

    [Column("new_danger_ratio", TypeName = "numeric(5,4)")]
    public decimal? NewDangerRatio { get; set; }

    [Column("new_warning_ratio", TypeName = "numeric(5,4)")]
    public decimal? NewWarningRatio { get; set; }

    [Column("changed_by")]
    public Guid ChangedBy { get; set; }

    [Column("changed_at", TypeName = "timestamp with time zone")]
    public DateTime ChangedAt { get; set; }

    [Column("change_reason")]
    [StringLength(500)]
    public string? ChangeReason { get; set; }

    [Column("action")]
    [StringLength(20)]
    public string Action { get; set; } = string.Empty;

    [ForeignKey(nameof(ConfigId))]
    public InventoryStockThresholdConfig? Config { get; set; }
}
