using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RESQ.Infrastructure.Entities.Logistics;

[Table("supply_inventory")]
public partial class SupplyInventory
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("depot_id")]
    public int? DepotId { get; set; }

    [Column("item_model_id")]
    public int? ItemModelId { get; set; }

    [Column("quantity")]
    public int? Quantity { get; set; }

    /// <summary>
    /// Số lượng đặt trước cho nhiệm vụ cứu hộ.
    /// Cùng với TransferReservedQuantity là nguồn dữ liệu duy nhất;
    /// không bao giờ cập nhật trực tiếp cột reserved_quantity cũ.
    /// </summary>
    [Column("mission_reserved_quantity")]
    public int MissionReservedQuantity { get; set; }

    /// <summary>
    /// Số lượng đặt trước cho yêu cầu tiếp tế giữa kho.
    /// Cùng với MissionReservedQuantity là nguồn dữ liệu duy nhất.
    /// </summary>
    [Column("transfer_reserved_quantity")]
    public int TransferReservedQuantity { get; set; }

    /// <summary>
    /// Tổng số lượng đặt trước = MissionReservedQuantity + TransferReservedQuantity.
    /// Chỉ được tính, không lưu xuống DB.
    /// </summary>
    [NotMapped]
    public int TotalReservedQuantity => MissionReservedQuantity + TransferReservedQuantity;

    [Column("last_stocked_at", TypeName = "timestamp with time zone")]
    public DateTime? LastStockedAt { get; set; }

    [Column("is_deleted")]
    public bool IsDeleted { get; set; } = false;

    [ForeignKey("DepotId")]
    [InverseProperty("SupplyInventories")]
    public virtual Depot? Depot { get; set; }

    [ForeignKey("ItemModelId")]
    [InverseProperty("SupplyInventories")]
    public virtual ItemModel? ItemModel { get; set; }

    [InverseProperty("SupplyInventory")]
    public virtual ICollection<InventoryLog> InventoryLogs { get; set; } = new List<InventoryLog>();

    [InverseProperty("SupplyInventory")]
    public virtual ICollection<SupplyInventoryLot> Lots { get; set; } = new List<SupplyInventoryLot>();
}
