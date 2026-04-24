using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RESQ.Infrastructure.Entities.Logistics;

/// <summary>
/// Bản ghi kiểm toán cho từng dòng hàng tồn kho được xử lý bên ngoài khi đóng kho.
/// Mỗi dòng tương ứng với một lot consumable hoặc một reusable unit mà depot manager đã xử lý
/// và ghi nhận vào file Excel rồi upload lên server.
/// </summary>
[Table("depot_closure_external_items")]
public class DepotClosureExternalItem
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("depot_id")]
    public int DepotId { get; set; }

    [Column("closure_id")]
    public int? ClosureId { get; set; }

    [Column("item_model_id")]
    public int? ItemModelId { get; set; }

    [Column("lot_id")]
    public int? LotId { get; set; }

    [Column("reusable_item_id")]
    public int? ReusableItemId { get; set; }

    [Column("item_name")]
    [StringLength(255)]
    public string ItemName { get; set; } = string.Empty;

    [Column("category_name")]
    [StringLength(255)]
    public string? CategoryName { get; set; }

    [Column("item_type")]
    [StringLength(50)]
    public string ItemType { get; set; } = string.Empty;

    [Column("unit")]
    [StringLength(50)]
    public string? Unit { get; set; }

    [Column("serial_number")]
    [StringLength(100)]
    public string? SerialNumber { get; set; }

    [Column("quantity")]
    public int Quantity { get; set; }

    [Column("unit_price", TypeName = "numeric(18,2)")]
    public decimal? UnitPrice { get; set; }

    [Column("total_price", TypeName = "numeric(18,2)")]
    public decimal? TotalPrice { get; set; }

    [Column("handling_method")]
    [StringLength(100)]
    public string HandlingMethod { get; set; } = string.Empty;

    [Column("recipient")]
    [StringLength(255)]
    public string? Recipient { get; set; }

    [Column("note")]
    public string? Note { get; set; }

    [Column("image_url")]
    [StringLength(2048)]
    public string? ImageUrl { get; set; }

    [Column("processed_by")]
    public Guid ProcessedBy { get; set; }

    [Column("processed_at", TypeName = "timestamp with time zone")]
    public DateTime ProcessedAt { get; set; }

    [Column("created_at", TypeName = "timestamp with time zone")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(DepotId))]
    public virtual Depot? Depot { get; set; }

    [ForeignKey(nameof(ItemModelId))]
    public virtual ItemModel? ItemModel { get; set; }

    [ForeignKey(nameof(ClosureId))]
    public virtual DepotClosure? DepotClosure { get; set; }

    [ForeignKey(nameof(LotId))]
    public virtual SupplyInventoryLot? Lot { get; set; }

    [ForeignKey(nameof(ReusableItemId))]
    public virtual ReusableItem? ReusableItem { get; set; }
}
