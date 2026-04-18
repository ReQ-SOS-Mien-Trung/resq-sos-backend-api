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

    [Column("item_model_id")]
    public int? ItemModelId { get; set; }

    [Column("mission_id")]
    public int? MissionId { get; set; }

    [Column("required_quantity")]
    public int? RequiredQuantity { get; set; }

    [Column("allocated_quantity")]
    public int? AllocatedQuantity { get; set; }

    [Column("source_depot_id")]
    public int? SourceDepotId { get; set; }

    [Column("buffer_ratio")]
    public double? BufferRatio { get; set; }

    [ForeignKey("MissionId")]
    [InverseProperty("MissionItems")]
    public virtual Mission? Mission { get; set; }

    [ForeignKey("ItemModelId")]
    [InverseProperty("MissionItems")]
    public virtual ItemModel? ItemModel { get; set; }

    [ForeignKey("SourceDepotId")]
    [InverseProperty("MissionItems")]
    public virtual Depot? SourceDepot { get; set; }
}
