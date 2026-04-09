using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RESQ.Infrastructure.Entities.Logistics;

/// <summary>
/// Bản ghi kiểm toán cho từng dòng hàng tồn kho được xử lý bên ngoài khi đóng kho.
/// Mỗi dòng tương ứng với một loại vật phẩm (item_model) mà depot manager đã xử lý
/// và ghi nhận vào file Excel, rồi upload lên server.
/// </summary>
[Table("depot_closure_external_items")]
public class DepotClosureExternalItem
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    /// <summary>FK đến depot đang đóng.</summary>
    [Column("depot_id")]
    public int DepotId { get; set; }

    /// <summary>FK đến bản ghi đóng kho (tạo sau khi upload thành công).</summary>
    [Column("closure_id")]
    public int? ClosureId { get; set; }

    /// <summary>FK đến item_model — nullable nếu item không khớp model nào.</summary>
    [Column("item_model_id")]
    public int? ItemModelId { get; set; }

    [Column("item_name")]
    [StringLength(255)]
    public string ItemName { get; set; } = string.Empty;

    [Column("category_name")]
    [StringLength(255)]
    public string? CategoryName { get; set; }

    /// <summary>"Consumable" hoặc "Reusable".</summary>
    [Column("item_type")]
    [StringLength(50)]
    public string ItemType { get; set; } = string.Empty;

    [Column("unit")]
    [StringLength(50)]
    public string? Unit { get; set; }

    [Column("quantity")]
    public int Quantity { get; set; }

    /// <summary>Đơn giá vật phẩm (nếu thanh lý/bán).</summary>
    [Column("unit_price", TypeName = "numeric(18,2)")]
    public decimal? UnitPrice { get; set; }

    /// <summary>Thành tiền = Số lượng × Đơn giá.</summary>
    [Column("total_price", TypeName = "numeric(18,2)")]
    public decimal? TotalPrice { get; set; }

    /// <summary>Cách xử lý: Donated, Disposed, Sold, ReturnedToSupplier (hoặc nhập tay nếu có hình thức khác).</summary>
    [Column("handling_method")]
    [StringLength(100)]
    public string HandlingMethod { get; set; } = string.Empty;

    /// <summary>Người / tổ chức nhận hàng (nếu có).</summary>
    [Column("recipient")]
    [StringLength(255)]
    public string? Recipient { get; set; }

    [Column("note")]
    public string? Note { get; set; }

    /// <summary>URL ảnh bằng chứng (upload riêng, không có trong Excel).</summary>
    [Column("image_url")]
    [StringLength(2048)]
    public string? ImageUrl { get; set; }

    /// <summary>Ai đã upload / xử lý bản ghi này.</summary>
    [Column("processed_by")]
    public Guid ProcessedBy { get; set; }

    [Column("processed_at", TypeName = "timestamp with time zone")]
    public DateTime ProcessedAt { get; set; }

    [Column("created_at", TypeName = "timestamp with time zone")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey("DepotId")]
    public virtual Depot? Depot { get; set; }

    [ForeignKey("ItemModelId")]
    public virtual ItemModel? ItemModel { get; set; }

    [ForeignKey("ClosureId")]
    public virtual DepotClosure? DepotClosure { get; set; }
}
