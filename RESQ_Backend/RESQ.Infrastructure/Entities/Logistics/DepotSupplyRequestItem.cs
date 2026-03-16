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

    [Column("relief_item_id")]
    public int ReliefItemId { get; set; }

    [Column("quantity")]
    public int Quantity { get; set; }

    [ForeignKey("DepotSupplyRequestId")]
    [InverseProperty("Items")]
    public virtual DepotSupplyRequest DepotSupplyRequest { get; set; } = null!;

    [ForeignKey("ReliefItemId")]
    [InverseProperty("DepotSupplyRequestItems")]
    public virtual ReliefItem ReliefItem { get; set; } = null!;
}
