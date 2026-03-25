using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RESQ.Infrastructure.Entities.Logistics;

[Table("inventory_stock_threshold_configs")]
public class InventoryStockThresholdConfig
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("scope_type")]
    [StringLength(32)]
    public string ScopeType { get; set; } = string.Empty;

    [Column("depot_id")]
    public int? DepotId { get; set; }

    [Column("category_id")]
    public int? CategoryId { get; set; }

    [Column("item_model_id")]
    public int? ItemModelId { get; set; }

    [Column("danger_ratio", TypeName = "numeric(5,4)")]
    public decimal DangerRatio { get; set; }

    [Column("warning_ratio", TypeName = "numeric(5,4)")]
    public decimal WarningRatio { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("updated_by")]
    public Guid? UpdatedBy { get; set; }

    [Column("updated_at", TypeName = "timestamp with time zone")]
    public DateTime UpdatedAt { get; set; }

    [Column("row_version")]
    public uint RowVersion { get; set; }

    [ForeignKey(nameof(DepotId))]
    public Depot? Depot { get; set; }

    [ForeignKey(nameof(CategoryId))]
    public Category? Category { get; set; }

    [ForeignKey(nameof(ItemModelId))]
    public ItemModel? ItemModel { get; set; }
}
