using RESQ.Infrastructure.Entities.Logistics;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RESQ.Infrastructure.Entities.Operations;

[Table("mission_items")]
public partial class MissionItem
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("relief_item_id")]
    public int? ReliefItemId { get; set; }

    [Column("mission_id")]
    public int? MissionId { get; set; }

    [Column("required_quantity")]
    public int? RequiredQuantity { get; set; }

    [Column("allocated_quantity")]
    public int? AllocatedQuantity { get; set; }

    [Column("source_depot_id")]
    public int? SourceDepotId { get; set; }

    [ForeignKey("MissionId")]
    [InverseProperty("MissionItems")]
    public virtual Mission? Mission { get; set; }

    [ForeignKey("ReliefItemId")]
    [InverseProperty("MissionItems")]
    public virtual ReliefItem? ReliefItem { get; set; }

    [ForeignKey("SourceDepotId")]
    [InverseProperty("MissionItems")]
    public virtual Depot? SourceDepot { get; set; }
}
