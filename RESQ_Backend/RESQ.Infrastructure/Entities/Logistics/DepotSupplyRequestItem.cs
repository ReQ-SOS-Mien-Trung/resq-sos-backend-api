using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RESQ.Infrastructure.Entities.Logistics;

[Table("depot_supply_request_items")]
public partial class DepotSupplyRequestItem
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("depot_supply_request_id")]
    public int DepotSupplyRequestId { get; set; }

    [Column("item_model_id")]
    public int ItemModelId { get; set; }

    [Column("quantity")]
    public int Quantity { get; set; }

    [ForeignKey("DepotSupplyRequestId")]
    [InverseProperty("Items")]
    public virtual DepotSupplyRequest DepotSupplyRequest { get; set; } = null!;

    [ForeignKey("ItemModelId")]
    [InverseProperty("DepotSupplyRequestItems")]
    public virtual ItemModel ItemModel { get; set; } = null!;
}
